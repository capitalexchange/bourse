// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.TxPool.Collections;

namespace Nethermind.TxPool.Filters
{
    /// <summary>
    /// Filters out transactions where gas fee properties were set too low.
    /// </summary>
    internal sealed class FeeTooLowFilter(IChainHeadInfoProvider headInfo, TxDistinctSortedPool txs, TxDistinctSortedPool blobTxs, bool thereIsPriorityContract, ILogger logger) : IIncomingTxFilter
    {
        private readonly IChainHeadSpecProvider _specProvider = headInfo.SpecProvider;
        private readonly IChainHeadInfoProvider _headInfo = headInfo;
        private readonly TxDistinctSortedPool _txs = txs;
        private readonly TxDistinctSortedPool _blobTxs = blobTxs;
        private readonly bool _thereIsPriorityContract = thereIsPriorityContract;
        private readonly ILogger _logger = logger;

        public AcceptTxResult Accept(Transaction tx, ref TxFilteringState state, TxHandlingOptions handlingOptions)
        {
            bool isLocal = (handlingOptions & TxHandlingOptions.PersistentBroadcast) != 0;
            if (isLocal)
            {
                return AcceptTxResult.Accepted;
            }

            IReleaseSpec spec = _specProvider.GetCurrentHeadSpec();
            bool isEip1559Enabled = spec.IsEip1559Enabled;
            UInt256 affordableGasPrice = tx.CalculateGasPrice(isEip1559Enabled, _headInfo.CurrentBaseFee);

            // Bourse fork: the upstream rejection of `affordableGasPrice == 0` was an anti-spam
            // heuristic for public chains where a zero-priority tx pays nothing to the miner and
            // can sit in the pool forever. On Bourse the base fee is pinned at 1 wei and the
            // basefee is redirected to the block beneficiary (commit f73bfbf3a4), so even a tx
            // with priorityFee=0 still pays `1 wei × gasUsed` to the validator and is genuinely
            // includable. The eth_sendTransaction guard from 5274565f19 still rejects txs whose
            // maxFeePerGas is below the current baseFee, so zero-fee txs never reach this point.
            // Drop the IsZero rejection so a wallet that fills `maxFee=1, priority=0` (matching
            // what the patched GasPriceOracle suggests) is accepted into the pool.

            TxDistinctSortedPool relevantPool = (tx.SupportsBlobs ? _blobTxs : _txs);
            if (relevantPool.IsFull() && relevantPool.TryGetLast(out Transaction? lastTx)
                && affordableGasPrice <= lastTx?.GasBottleneck)
            {
                Metrics.PendingTransactionsTooLowFee++;
                if (_logger.IsTrace)
                {
                    _logger.Trace($"Skipped adding transaction {tx.ToString("  ")}, too low payable gas price with options {handlingOptions} from {new StackTrace()}");
                }

                return AcceptTxResult.FeeTooLow;
            }

            return AcceptTxResult.Accepted;
        }
    }
}
