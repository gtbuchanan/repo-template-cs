using AutoFixture;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace RepoTemplate.Test.Customizations
{
    public sealed class DomainCustomizationTest
    {
        [Test]
        public void SubstitutesInterfaces()
        {
            var fixture = new Fixture().Customize(new DomainCustomization());

            Should.NotThrow(() =>
            {
                var test = fixture.Create<ITest>();
                test.Test.Returns(default(string));
            });
        }

        internal interface ITest
        {
            string Test { get; }
        }
    }
}
