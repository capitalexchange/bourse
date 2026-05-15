// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Core
{
    public class Eip1559Constants
    {

        /// <summary>Absolute lower bound the EIP-1559 base fee may decay to once EIP-1559 is active.</summary>
        public static readonly UInt256 MinimumBaseFee = 1;

        /// <summary>
        /// Base fee assigned at the EIP-1559 fork block (or at genesis when EIP-1559 is active from
        /// block 0 and the genesis file doesn't override it).
        /// </summary>
        /// <remarks>
        /// Bourse fork: pinned to <see cref="MinimumBaseFee"/> (1 wei) instead of the canonical
        /// 1 gwei so the private chain boots at 1 wei and the under-target decay branch + the
        /// upward-branch clamp in <see cref="DefaultBaseFeeCalculator"/> keep it there. This makes
        /// gas effectively free without zeroing the base fee (which would disable EIP-1559's
        /// effective-priority-fee semantics).
        /// </remarks>
        public static readonly UInt256 DefaultForkBaseFee = MinimumBaseFee;

        public static readonly UInt256 DefaultBaseFeeMaxChangeDenominator = 8;

        public static readonly int DefaultElasticityMultiplier = 2;
    }
}
