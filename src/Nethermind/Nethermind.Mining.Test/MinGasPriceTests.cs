// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Config;
using Nethermind.Consensus.Transactions;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Specs;
using Nethermind.TxPool;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.Mining.Test
{
    [TestFixture]
    public class MinGasPriceTests
    {
        [TestCase(0L, 0L, true)]
        [TestCase(1L, 0L, false)]
        [TestCase(1L, 1L, true)]
        [TestCase(1L, 2L, true)]
        [TestCase(2L, 1L, false)]
        public void Test(long minimum, long actual, bool expectedResult)
        {
            IReleaseSpec releaseSpec = new ReleaseSpec()
            {
                IsEip1559Enabled = false
            };

            BlocksConfig blocksConfig = new()
            {
                MinGasPrice = (UInt256)minimum
            };

            MinGasPriceTxFilter filter = new(blocksConfig);
            Transaction tx = Build.A.Transaction.WithGasPrice((UInt256)actual).TestObject;
            filter.IsAllowed(tx, null!, releaseSpec).Equals(expectedResult ? AcceptTxResult.Accepted : AcceptTxResult.FeeTooLow).Should().BeTrue();
        }

        // Bourse fork: the filter calls BaseFeeCalculator.Calculate(parent, spec), and on Bourse
        // that always returns Eip1559Constants.MinimumBaseFee (2_380_952_381). The test's tiny
        // parent baseFee=1000 is ignored. So premiumPerGas = max(0, maxFee - 2.38B) = 0 for every
        // case here where maxFee ≤ 2.38B. Expected truth values now reduce to "0 >= minimum".
        // (Cases that previously expected `true` because of the canonical 1000-baseFee math —
        // (1, 876, 1000) and (2, 1000, 1000) — now expect `false` because the pin swallows the
        // premium.)
        [TestCase(0L, 0L, 0L, true)]    // 0 premium ≥ 0 minimum
        [TestCase(1L, 0L, 0L, false)]
        [TestCase(1L, 0L, 1L, false)]
        [TestCase(1L, 100L, 1000L, false)]
        [TestCase(1L, 875L, 1000L, false)]
        [TestCase(1L, 876L, 1000L, false)] // was true (canonical), now false (pin swallows)
        [TestCase(1L, 876L, 0L, false)]
        [TestCase(2L, 1000L, 1L, false)]
        [TestCase(2L, 1000L, 1000L, false)] // was true (canonical), now false (pin swallows)
        public void Test1559(long minimum, long maxFeePerGas, long maxPriorityFeePerGas, bool expectedResult)
        {
            ISpecProvider specProvider = Substitute.For<ISpecProvider>();
            specProvider.GetSpec(Arg.Any<ForkActivation>()).IsEip1559Enabled.Returns(true);
            specProvider.GetSpec(Arg.Any<ForkActivation>()).BaseFeeCalculator.Returns(new DefaultBaseFeeCalculator());

            specProvider.GetSpec(Arg.Any<ForkActivation>()).ForkBaseFee.Returns(Eip1559Constants.DefaultForkBaseFee);
            specProvider.GetSpec(Arg.Any<ForkActivation>()).BaseFeeMaxChangeDenominator.Returns(Eip1559Constants.DefaultBaseFeeMaxChangeDenominator);
            specProvider.GetSpec(Arg.Any<ForkActivation>()).ElasticityMultiplier.Returns(Eip1559Constants.DefaultElasticityMultiplier);

            BlocksConfig blocksConfig = new()
            {
                MinGasPrice = (UInt256)minimum
            };
            MinGasPriceTxFilter _filter = new(blocksConfig);
            Transaction tx = Build.A.Transaction.WithGasPrice(0)
                .WithMaxFeePerGas((UInt256)maxFeePerGas)
                .WithMaxPriorityFeePerGas((UInt256)maxPriorityFeePerGas)
                .WithType(TxType.EIP1559).TestObject;
            BlockBuilder blockBuilder = Core.Test.Builders.Build.A.Block.Genesis.WithGasLimit(10000).WithBaseFeePerGas((UInt256)1000);
            _filter.IsAllowed(tx, blockBuilder.TestObject.Header, specProvider.GetSpec(blockBuilder.TestObject.Header)).Equals(expectedResult ? AcceptTxResult.Accepted : AcceptTxResult.FeeTooLow).Should().BeTrue();
        }
    }
}
