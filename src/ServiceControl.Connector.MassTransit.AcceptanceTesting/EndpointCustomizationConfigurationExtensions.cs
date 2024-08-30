﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Hosting.Helpers;

public static class EndpointCustomizationConfigurationExtensions
{
    public static IEnumerable<Type> GetTypesScopedByTestClass(this EndpointCustomizationConfiguration endpointConfiguration)
    {
        var assemblyScanner = new AssemblyScanner
        {
            ScanFileSystemAssemblies = false
        };

        var assemblies = assemblyScanner.GetScannableAssemblies();

        var assembliesToScan = assemblies.Assemblies
            //exclude acceptance tests by default
            .Where(a => a != Assembly.GetExecutingAssembly()).ToList();
        var types = assembliesToScan
            .SelectMany(a => a.GetTypes());

        var testTypes = GetNestedTypeRecursive(endpointConfiguration.BuilderType.DeclaringType, endpointConfiguration.BuilderType);

        types = types.Union(testTypes);

        types = types.Union(endpointConfiguration.TypesToInclude);

        return types.Where(t => !endpointConfiguration.TypesToExclude.Contains(t)).ToList();
    }

    static IEnumerable<Type> GetNestedTypeRecursive(Type rootType, Type builderType)
    {
        if (rootType == null)
        {
            throw new InvalidOperationException("Make sure you nest the endpoint infrastructure inside the TestFixture as nested classes");
        }

        yield return rootType;

        if (typeof(IEndpointConfigurationFactory).IsAssignableFrom(rootType) && rootType != builderType)
        {
            yield break;
        }

        foreach (var nestedType in rootType.GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SelectMany(t => GetNestedTypeRecursive(t, builderType)))
        {
            yield return nestedType;
        }
    }
}
