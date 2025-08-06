// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SceneNavigation.Extensions
{
    public static class AddressableExtensions
    {
        public static async UniTask<AsyncHandleSnapshot<IResourceLocator>> Initialize()
        {
            var handle = Addressables.InitializeAsync(false);
            var snapshot = await handle.CaptureWithRelease();

            return snapshot;
        }

        public static async UniTask<AsyncHandleSnapshot<List<string>>> CheckForCatalogUpdates()
        {
            var handle = Addressables.CheckForCatalogUpdates(false);
            var snapshot = await handle.CaptureWithRelease();
            if (snapshot.Status == AsyncOperationStatus.Succeeded)
            {
                return snapshot;
            }

            var ex = snapshot.OperationException;
            var wrappedException = ex.Message switch
            {
                // { } msg when msg.Contains("RemoteProviderException") => new RemoteProviderException(ex.Message),
                // { } msg when msg.Contains("ConnectionError") => new CatalogUpdateConnectionException(ex),
                _ => ex,
            };
            // throw wrappedException;
            snapshot = new AsyncHandleSnapshot<List<string>>()
            {
                Status = snapshot.Status,
                Result = snapshot.Result,
                OperationException = wrappedException,
                DebugName = snapshot.DebugName
            };

            return snapshot;
        }

        public static async UniTask<AsyncHandleSnapshot<T>> CaptureWithRelease<T>(this AsyncOperationHandle<T> handle)
        {
            await handle;
            AsyncHandleSnapshot<T> snapshot = new()
            {
                Status = handle.Status,
                Result = handle.Result,
                OperationException = handle.OperationException,
                DebugName = handle.DebugName
            };
            handle.Release();
            return snapshot;
        }

        public readonly struct AsyncHandleSnapshot<T>
        {
            public AsyncOperationStatus Status { get; init; }
            public T Result { get; init; }
            public Exception OperationException { get; init; }
            public string DebugName { get; init; }

            public bool IsSuccess => Status == AsyncOperationStatus.Succeeded;

            public void Deconstruct(out AsyncOperationStatus status, out T result, out Exception operationException,
                out string debugName)
            {
                status = Status;
                result = Result;
                operationException = OperationException;
                debugName = DebugName;
            }
        }

        public class CatalogUpdateConnectionException : Exception
        {
            public CatalogUpdateConnectionException(Exception innerException) : base(innerException.Message,
                innerException)
            {
            }
        }
    }
}