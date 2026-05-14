// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Core
{
    public class Eip1559Constants
    {

        public static readonly UInt256 DefaultForkBaseFee = 1.GWei;

        /// <summary>Absolute lower bound the EIP-1559 base fee may decay to once EIP-1559 is active.</summary>
        public static readonly UInt256 MinimumBaseFee = 1;

        public static readonly UInt256 DefaultBaseFeeMaxChangeDenominator = 8;

        public static readonly int DefaultElasticityMultiplier = 2;
    }
}
