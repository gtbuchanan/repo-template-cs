using AutoFixture;
using JetBrains.Annotations;
using NSubstitute;
using NUnit.Framework;
using Shouldly;
using System;

namespace RepoTemplate.Test.Customizations
{
    public sealed class DomainCustomizationTest
    {
        [Test]
        public void SubstitutesInterfaces()
        {
            var fixture = CreateFixture();

            Should.NotThrow(() =>
            {
                var test = fixture.Create<ITest>();
                test.Test.Returns(default(string));
            });
        }

        [Test]
        public void SubstitutesDelegates()
        {
            var fixture = CreateFixture();

            Should.NotThrow(() =>
            {
                var action = fixture.Create<Func<bool>>();
                action.Invoke().Returns(true);
            });
        }

        private static IFixture CreateFixture() => new Fixture().Customize(new DomainCustomization());

        [UsedImplicitly]
        internal interface ITest
        {
            string Test { get; }
        }
    }
}
