// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Core
{
    public class Eip1559Constants
    {

        /// <summary>
        /// Absolute lower bound the EIP-1559 base fee may decay to once EIP-1559 is active.
        /// </summary>
        /// <remarks>
        /// Bourse fork: this is also the chain's pinned flat base fee. The over-target and
        /// under-target clamps in <see cref="DefaultBaseFeeCalculator"/> both drive the result to
        /// this value, so every block's <c>baseFeePerGas</c> equals <c>MinimumBaseFee</c>.
        /// <para>
        /// Value: <c>714_285_714_285</c> wei = <c>0xA64EBF0B6D</c>. At a 21,000-gas standard
        /// transfer this works out to <c>714_285_714_285 × 21_000 = 14_999_999_999_985_000</c> wei
        /// ≈ <c>0.015</c> BOURSE — chosen as the largest integer wei/gas value whose 21k-gas
        /// fee is ≤ 0.015 BOURSE. At an anticipated BOURSE/USD between $0.10 and $0.16 this
        /// puts a standard transfer at ≈ $0.0015 – $0.0024 (modern-L2 economics).
        /// </para>
        /// <para>
        /// Bumping this is forward-only: existing sealed blocks keep their old <c>baseFeePerGas</c>
        /// in their (immutable) RLP headers, and the next block produced under the new binary
        /// jumps to the new value via the same clamp the validator agrees on. No chain reset.
        /// </para>
        /// </remarks>
        public static readonly UInt256 MinimumBaseFee = 714_285_714_285;

        /// <summary>
        /// Base fee assigned at the EIP-1559 fork block (or at genesis when EIP-1559 is active from
        /// block 0 and the genesis file doesn't override it).
        /// </summary>
        /// <remarks>
        /// Bourse fork: pinned to <see cref="MinimumBaseFee"/> so a fresh chain starts at the flat
        /// rate immediately rather than the canonical 1 gwei. The calculator clamps then keep every
        /// subsequent block at the same value.
        /// </remarks>
        public static readonly UInt256 DefaultForkBaseFee = MinimumBaseFee;

        /// <summary>
        /// Bourse fork: flat fee paid by the sender per transaction, in wei, regardless of how much
        /// gas the tx actually consumed.
        /// </summary>
        /// <remarks>
        /// Value: <c>15_000_000_000_000_000</c> wei = <c>0.015</c> BOURSE. At an anchor price of
        /// <c>1 BOURSE = 0.01 EUR</c>, every transaction on Bourse costs <c>€0.00015</c> — Solana-tier
        /// per-tx economics, well under Visa interchange (~30¢), Stripe (~30¢ + 2.9%), or any other
        /// EVM L1.
        /// <para>
        /// Relationship to <see cref="MinimumBaseFee"/>: <c>FlatFee ≈ MinimumBaseFee × 21_000</c>
        /// (target equality, off by 15_000 wei truncation error in <c>MinimumBaseFee</c>). The pin was
        /// chosen so that a 21k-gas standard transfer at <c>gasPrice = MinimumBaseFee</c> reserves
        /// almost exactly <c>FlatFee</c>, which means wallets that autofill
        /// <c>maxFeePerGas = eth_gasPrice = head.BaseFeePerGas</c> display the right number for the
        /// common transfer case. Heavier transactions (contract calls, bridge deliveries) reserve
        /// more upfront and are refunded the excess in <c>PayFees</c>, so the user always pays exactly
        /// <c>FlatFee</c> end-to-end.
        /// </para>
        /// <para>
        /// Wired up in <c>Nethermind.Evm.TransactionProcessing.TransactionProcessor.PayFees</c>:
        /// after the standard EIP-1559 settlement debits the sender <c>spentGas × baseFeePerGas</c>,
        /// the difference vs <c>FlatFee</c> is refunded (heavy tx) or charged (cheap tx), and the
        /// beneficiary is credited <c>FlatFee</c> instead of the gas-proportional amount. EVM gas
        /// accounting itself is unchanged — gas counters, <c>gasleft()</c>, OOG reverts, and the block
        /// gas limit all keep their EVM semantics; this only changes what the validator collects and
        /// the sender pays at settlement time.
        /// </para>
        /// </remarks>
        public static readonly UInt256 FlatFee = 15_000_000_000_000_000;

        public static readonly UInt256 DefaultBaseFeeMaxChangeDenominator = 8;

        public static readonly int DefaultElasticityMultiplier = 2;
    }
}
