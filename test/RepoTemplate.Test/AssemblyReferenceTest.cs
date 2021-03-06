﻿using NUnit.Framework;
using Shouldly;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace RepoTemplate.Test
{
    [ExcludeFromCodeCoverage]
    public sealed class AssemblyReferenceTest
    {
        [Test]
        public void DoesNotReferenceJetBrainsAnnotations() =>
            typeof(ThisAssembly).Assembly
                .GetReferencedAssemblies()
                .Select(a => a.Name)
                .ShouldNotContain("JetBrains.Annotations");
    }
}
