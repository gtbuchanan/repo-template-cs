using AutoFixture;
using AutoFixture.NUnit3;
using RepoTemplate.Test.Customizations;
using System.Diagnostics.CodeAnalysis;

namespace RepoTemplate.Test
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    internal sealed class AutoDomainDataAttribute : AutoDataAttribute
    {
        /// <inheritdoc />
        public AutoDomainDataAttribute() : base(FixtureFactory) { }

        private static IFixture FixtureFactory() => new Fixture().Customize(new DomainCustomization());
    }
}
