// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Config;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.TxPool;

namespace Nethermind.Consensus.Processing
{
    public partial class BlockProcessor
    {
        public class BlockProductionTransactionPicker(
            ISpecProvider specProvider,
            long maxTxLengthKilobytes = BlocksConfig.DefaultMaxTxKilobytes,
            bool ignoreEip3607 = false)
            : IBlockProductionTransactionPicker
        {
            private readonly long _maxTxLengthBytes = maxTxLengthKilobytes.KiB;

            protected readonly ISpecProvider _specProvider = specProvider;

            public event EventHandler<AddingTxEventArgs>? AddingTransaction;

            protected void OnAddingTransaction(AddingTxEventArgs e) => AddingTransaction?.Invoke(this, e);

            public virtual AddingTxEventArgs CanAddTransaction(Block block, Transaction currentTx, IReadOnlySet<Transaction> transactionsInBlock, IReadOnlyStateProvider stateProvider)
            {
                AddingTxEventArgs args = new(transactionsInBlock.Count, currentTx, block, transactionsInBlock);

                long gasRemaining = block.Header.GasLimit - block.GasUsed;

                // No more gas available in block for any transactions,
                // the only case we have to really stop
                if (GasCostOf.Transaction > gasRemaining)
                {
                    return args.Set(TxAction.Stop, "Block full");
                }

                if (block is BlockToProduce blockToProduce && blockToProduce.TxByteLength + currentTx.GetLength(false) > _maxTxLengthBytes)
                {
                    return args.Set(
                        // If smallest tx is too large, stop picking
                        currentTx.GasLimit == GasCostOf.Transaction ? TxAction.Stop : TxAction.Skip,
                        "Too large for CL");
                }

                if (currentTx.SenderAddress is null)
                {
                    return args.Set(TxAction.Skip, "Null sender");
                }

                if (currentTx.GasLimit > gasRemaining)
                {
                    return args.Set(TxAction.Skip, $"Not enough gas in block, gas limit {currentTx.GasLimit} > {gasRemaining}");
                }

                if (transactionsInBlock.Contains(currentTx))
                {
                    return args.Set(TxAction.Skip, "Transaction already in block");
                }

                IReleaseSpec spec = _specProvider.GetSpec(block.Header);
                if (currentTx.IsAboveInitCode(spec))
                {
                    return args.Set(TxAction.Skip, TransactionResult.TransactionSizeOverMaxInitCodeSize.ErrorDescription);
                }

                if (!ignoreEip3607 && stateProvider.IsInvalidContractSender(spec, currentTx.SenderAddress))
                {
                    return args.Set(TxAction.Skip, $"Sender is contract");
                }

                UInt256 expectedNonce = stateProvider.GetNonce(currentTx.SenderAddress);
                if (expectedNonce != currentTx.Nonce)
                {
                    return args.Set(TxAction.Skip, $"Invalid nonce - expected {expectedNonce}");
                }

                UInt256 balance = stateProvider.GetBalance(currentTx.SenderAddress);
                if (!HasEnoughFunds(currentTx, balance, args, block, spec))
                {
                    return args;
                }

                OnAddingTransaction(args);
                return args;
            }

            private static bool HasEnoughFunds(Transaction transaction, in UInt256 senderBalance, AddingTxEventArgs e, Block block, IReleaseSpec releaseSpec)
            {
                bool eip1559Enabled = releaseSpec.IsEip1559Enabled;
                UInt256 transactionPotentialCost = transaction.CalculateTransactionPotentialCost(eip1559Enabled, block.BaseFeePerGas);

                if (senderBalance < transactionPotentialCost)
                {
                    e.Set(TxAction.Skip, $"Transaction cost ({transactionPotentialCost}) is higher than sender balance ({senderBalance})");
                    return false;
                }

                if (!transaction.IsServiceTransaction && eip1559Enabled)
                {
                    UInt256 maxFee = (UInt256)transaction.GasLimit * transaction.MaxFeePerGas + transaction.Value;

                    if (senderBalance < maxFee)
                    {
                        e.Set(TxAction.Skip, $"{maxFee} is higher than sender balance ({senderBalance}), MaxFeePerGas: ({transaction.MaxFeePerGas}), GasLimit {transaction.GasLimit}");
                        return false;
                    }

                    if (transaction.SupportsBlobs && (
                        !BlobGasCalculator.TryCalculateBlobBaseFee(block.Header, transaction, releaseSpec.BlobBaseFeeUpdateFraction, out UInt256 blobBaseFee) ||
                        senderBalance < (maxFee += blobBaseFee)))
                    {
                        e.Set(TxAction.Skip, $"{maxFee} is higher than sender balance ({senderBalance}), MaxFeePerGas: ({transaction.MaxFeePerGas}), GasLimit {transaction.GasLimit}, BlobBaseFee: {blobBaseFee}");
                        return false;
                    }

                    // Bourse fork: align the picker's affordability check with the executor's flat-fee
                    // settlement. When the block is sealed at the pinned baseFee, `TransactionProcessor.PayFees`
                    // charges `Eip1559Constants.FlatFee` per tx — which exceeds `gasLimit × maxFeePerGas` by 15_000
                    // wei for the canonical 21k transfer at `maxFeePerGas == MinimumBaseFee`. Without this check
                    // the picker would let a sender at exactly `balance == gasLimit × maxFeePerGas + value` through;
                    // PayFees would then try to subtract the deficit from a zero balance, throw
                    // `InsufficientBalanceException`, abort the entire candidate block, and (because the tx isn't
                    // evicted) halt production indefinitely as the producer re-selects the poison tx every cycle.
                    // Observed in production 2026-06-13 at block 78268. The defensive try/catch in
                    // BlockProductionTransactionsExecutor.ProcessTransaction is the backstop; this check is the
                    // proper fix that prevents the slip-through in the first place.
                    if (block.BaseFeePerGas == Eip1559Constants.MinimumBaseFee)
                    {
                        if (UInt256.AddOverflow(Eip1559Constants.FlatFee, transaction.Value, out UInt256 flatPotentialCost)
                            || senderBalance < flatPotentialCost)
                        {
                            e.Set(TxAction.Skip, $"Bourse flat-fee potential cost ({flatPotentialCost}) is higher than sender balance ({senderBalance})");
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        public enum TxAction
        {
            Add,
            Skip,
            Stop
        }
    }
}
