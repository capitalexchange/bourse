// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State.Proofs;
using Nethermind.TxPool;
using Nethermind.TxPool.Comparison;

namespace Nethermind.Consensus.Processing
{
    public partial class BlockProcessor
    {
        public class BlockProductionTransactionsExecutor(
            ITransactionProcessorAdapter transactionProcessor,
            IWorldState stateProvider,
            IBlockProductionTransactionPicker txPicker,
            ILogManager logManager,
            IBlockAccessListManager balManager)
            : IBlockProductionTransactionsExecutor
        {
            private readonly ILogger _logger = logManager.GetClassLogger<BlockProductionTransactionsExecutor>();

            protected EventHandler<TxProcessedEventArgs>? _transactionProcessed;

            event EventHandler<AddingTxEventArgs>? IBlockProductionTransactionsExecutor.AddingTransaction
            {
                add => txPicker.AddingTransaction += value;
                remove => txPicker.AddingTransaction -= value;
            }

            public void SetBlockExecutionContext(in BlockExecutionContext blockExecutionContext)
            {
                transactionProcessor.SetBlockExecutionContext(in blockExecutionContext);
                balManager.SetBlockExecutionContext(blockExecutionContext);
            }

            public virtual TxReceipt[] ProcessTransactions(Block block, ProcessingOptions processingOptions,
                BlockReceiptsTracer receiptsTracer, CancellationToken token = default)
            {
                balManager.NextTransaction();

                // We start with high number as don't want to resize too much
                const int defaultTxCount = 512;

                BlockToProduce? blockToProduce = block as BlockToProduce;

                // Don't use blockToProduce.Transactions.Count() as that would fully enumerate which is expensive
                int txCount = blockToProduce is not null ? defaultTxCount : block.Transactions.Length;
                IEnumerable<Transaction> transactions = blockToProduce?.Transactions ?? block.Transactions;

                using ArrayPoolListRef<Transaction> includedTx = new(txCount);

                HashSet<Transaction> consideredTx = new(ByHashTxComparer.Instance);
                int i = 0;
                foreach (Transaction currentTx in transactions)
                {
                    // Check if we have gone over time or the payload has been requested
                    if (token.IsCancellationRequested) break;

                    TxAction action = ProcessTransaction(block, currentTx, i++, receiptsTracer, processingOptions, consideredTx);
                    if (action == TxAction.Stop) break;

                    consideredTx.Add(currentTx);
                    if (action == TxAction.Add)
                    {
                        includedTx.Add(currentTx);
                        if (blockToProduce is not null)
                        {
                            blockToProduce.TxByteLength += currentTx.GetLength(false);
                        }
                    }
                }

                block.Header.TxRoot = TxTrie.CalculateRoot(includedTx.AsSpan());
                if (blockToProduce is not null)
                {
                    blockToProduce.Transactions = includedTx.ToArray();
                }
                return receiptsTracer.TxReceipts.ToArray();
            }

            private TxAction ProcessTransaction(
                Block block,
                Transaction currentTx,
                int index,
                BlockReceiptsTracer receiptsTracer,
                ProcessingOptions processingOptions,
                HashSet<Transaction> transactionsInBlock)
            {
                AddingTxEventArgs args = txPicker.CanAddTransaction(block, currentTx, transactionsInBlock, stateProvider);

                if (args.Action != TxAction.Add)
                {
                    if (_logger.IsDebug) DebugSkipReason(currentTx, args);
                }
                else
                {
                    ITransactionProcessorAdapter processor = balManager.Enabled ? balManager.GetTxProcessor() : transactionProcessor;
                    try
                    {
                        TransactionResult result = processor.ProcessTransaction(currentTx, receiptsTracer, processingOptions, stateProvider);

                        if (result)
                        {
                            _transactionProcessed?.Invoke(this,
                                new TxProcessedEventArgs(index, currentTx, block.Header, receiptsTracer.TxReceipts[index]));
                            balManager.NextTransaction();
                        }
                        else
                        {
                            balManager.Rollback();
                            args.Set(TxAction.Skip, result.ErrorDescription!);
                        }
                    }
                    catch (Nethermind.State.InsufficientBalanceException ex)
                    {
                        // Bourse fork: defense-in-depth. The picker is supposed to have already filtered
                        // unaffordable txs; if it didn't (picker/executor disagree at the affordability
                        // boundary, or sender balance changes between selection and execution), DO NOT let
                        // the exception propagate. Letting it bubble up aborts the whole candidate block,
                        // and the offending tx isn't evicted, so the producer re-selects and re-fails it
                        // every cycle — turning one unaffordable tx into an indefinite chain halt.
                        // Field-observed in production 2026-06-13 at block 78268; root cause was the
                        // BlockProductionTransactionPicker.HasEnoughFunds boundary mismatch with the
                        // 1.0.9 flat-fee patch's PayFees deficit charge. That picker mismatch is the
                        // proper fix; this catch is the backstop. Apply ONLY to the production executor —
                        // BlockValidationTransactionsExecutor must still throw, since on the validation
                        // path an unprocessable tx legitimately makes the received block invalid.
                        // EndTxTrace cleanup: TransactionProcessorAdapterExtensions.ProcessTransaction
                        // calls receiptsTracer.StartNewTxTrace *before* invoking the transaction
                        // processor's Execute. When Execute throws, the matching EndTxTrace never runs
                        // and the tracer stays mid-trace, which makes the next StartNewTxTrace assert
                        // or silently replace. Defensively close the trace here so the tracer state
                        // machine is consistent regardless of which tx threw.
                        if (_logger.IsWarn) _logger.Warn($"Producing: skipping unprocessable tx {currentTx.Hash} ({ex.Message}); continuing block production from the rest.");
                        try { receiptsTracer.EndTxTrace(); } catch { /* tracer may already be ended; safe to ignore */ }
                        balManager.Rollback();
                        args.Set(TxAction.Skip, ex.Message);
                    }
                }

                return args.Action;

                [MethodImpl(MethodImplOptions.NoInlining)]
                void DebugSkipReason(Transaction currentTx, AddingTxEventArgs args)
                    => _logger.Debug($"Skipping transaction {currentTx.ToShortString()} because: {args.Reason}.");
            }
        }
    }
}
