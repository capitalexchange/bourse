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
        /// Value: <c>2_380_952_381</c> wei = <c>0x8DEA733D</c>. At a 21,000-gas standard
        /// transfer this works out to <c>2_380_952_381 × 21_000 = 50_000_000_001_000</c> wei
        /// ≈ <c>0.00005</c> BOURSE — chosen as the smallest integer wei/gas value whose 21k-gas
        /// fee is ≥ 0.00005 BOURSE.
        /// </para>
        /// <para>
        /// Bumping this is forward-only: existing sealed blocks keep their old <c>baseFeePerGas</c>
        /// in their (immutable) RLP headers, and the next block produced under the new binary
        /// jumps to the new value via the same clamp the validator agrees on. No chain reset.
        /// </para>
        /// </remarks>
        public static readonly UInt256 MinimumBaseFee = 2_380_952_381;

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

        public static readonly UInt256 DefaultBaseFeeMaxChangeDenominator = 8;

        public static readonly int DefaultElasticityMultiplier = 2;
    }
}
