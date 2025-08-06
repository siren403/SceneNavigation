// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using SceneNavigation.Commands;
using VitalRouter;
using R3;
using VitalRouter.R3;

namespace SceneNavigation.R3
{
    public static class RouterExtensions
    {
        public static Task FirstAsync<TCommand>(this Router router, CancellationToken ct = default)
            where TCommand : struct, ICommand
        {
            return router.AsObservable<TCommand>().FirstAsync(ct);
        }

        public static Task PostStartUpAsync(this Router router, CancellationToken ct = default)
        {
            return router.FirstAsync<PostStartUpCommand>(ct);
        }
    }
}