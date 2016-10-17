﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RService.IO.Abstractions;

namespace RService.IO
{
    /// <summary>
    /// Main service to hold routing and web service details.
    /// </summary>
    public class RService
    {
        /// <summary>
        /// All discovered routes from services and their DTOs.
        /// </summary>
        public Dictionary<string, ServiceDef> Routes { get; protected set; }

        /// <summary>
        /// <see cref="Type"/>s of all the services in the <see cref="Assembly"/>s.
        /// </summary>
        public IEnumerable<Type> ServiceTypes => Routes.Values.Select(x => x.ServiceType);

        protected readonly RServiceOptions Options;
        protected static Regex RoutePathCleaner = new Regex(@"^[\/~]+", RegexOptions.Compiled);

        /// <summary>
        /// Constructs a RService service and scans for routes and services in assemblies.
        /// </summary>
        /// <param name="options">Configuration options including the assemblies to scan.</param>
        public RService(IOptions<RServiceOptions> options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            Routes = new Dictionary<string, ServiceDef>();
            Options = options.Value;

            foreach (var assembly in Options.ServiceAssemblies)
            {
                ScanAssemblyForRoutes(assembly);
            }
        }

        /// <summary>
        /// Scans an <see cref="Assembly"/> for services and routes.
        /// </summary>
        /// <param name="assembly">Assembly to scan.</param>
        protected void ScanAssemblyForRoutes(Assembly assembly)
        {
            var classes = assembly.GetTypes().Where(x => x.ImplementsInterface<IService>()).ToList();
            var methodsWithAttribute = classes.SelectMany(x => x.GetPublicMethods()).Where(x => x.HasAttribute<RouteAttribute>()).ToList();
            var methodsParamWithAttribute = classes.SelectMany(x => x.GetPublicMethods()).Where(x => x.HasParamWithAttribute<RouteAttribute>()).ToList();

            RegisterMethodRoutes(methodsWithAttribute);
            RegisterParameterRoutes(methodsParamWithAttribute);
        }

        private void RegisterParameterRoutes(List<MethodInfo> methodsParamWithAttribute)
        {
            methodsParamWithAttribute.ForEach(method =>
            {
                var methodType = method.DeclaringType;
                var paramType = method.GetParamWithAttribute<RouteAttribute>();
                var attrs = paramType?.GetAttributes<RouteAttribute>().ToList();
                attrs?.ForEach(a =>
                {
                    var route = (RouteAttribute) a;
                    route.Verbs.GetFlags().ForEach(x =>
                    {
                        var def = new ServiceDef
                        {
                            Route = route,
                            ServiceType = methodType,
                            ServiceMethod = DelegateFactory.GenerateMethodCall(method),
                            RequestDtoType = paramType
                        };
                        Routes.Add(BuildCompositKey(route.Path, x), def);
                    });
                });
            });
        }

        private void RegisterMethodRoutes(List<MethodInfo> methodsWithAttribute)
        {
            methodsWithAttribute.ForEach(method =>
            {
                var attrs = method.GetCustomAttributes<RouteAttribute>().ToList();
                var type = method.DeclaringType;
                attrs.ForEach(attr =>
                {
                    attr.Verbs.GetFlags().ForEach(x =>
                    {
                        var def = new ServiceDef
                        {
                            Route = attr,
                            ServiceType = type,
                            ServiceMethod = DelegateFactory.GenerateMethodCall(method)
                        };
                        Routes.Add(BuildCompositKey(attr.Path, x), def);
                    });
                });
            });
        }

        protected static string BuildCompositKey(string path, RestVerbs verb)
        {
            return $"{CleanRoutePath(path)}:{verb}";
        }

        protected static string CleanRoutePath(string value)
        {
            return RoutePathCleaner.Replace(value, string.Empty);
        }
    }
}