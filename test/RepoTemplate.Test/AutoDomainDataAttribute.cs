using AutoFixture;
using AutoFixture.NUnit3;
using RepoTemplate.Test.Customizations;

namespace RepoTemplate.Test
{
    /// <inheritdoc />
    internal sealed class AutoDomainDataAttribute : AutoDataAttribute
    {
        /// <inheritdoc />
        public AutoDomainDataAttribute() : base(FixtureFactory) { }

        private static IFixture FixtureFactory() => new Fixture().Customize(new DomainCustomization());
    }
}
