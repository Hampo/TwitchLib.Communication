﻿using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchLib.Communication.Events;
using TwitchLib.Communication.Interfaces;
using TwitchLib.Communication.Models;
using TwitchLib.Communication.Tests.Helpers;
using Xunit;

namespace TwitchLib.Communication.Tests.Clients
{
    /// <summary>
    ///     bundles <see cref="IClient"/>-Tests in one container
    /// </summary>
    /// <typeparam name="T">
    ///     <list type="bullet">
    ///         <see cref="Clients.TcpClient"/>
    ///     </list>
    ///     <list type="bullet">
    ///         <see cref="Clients.WebSocketClient"/>
    ///     </list>
    /// </typeparam>
    public abstract class ClientTestsBase<T> where T : IClient
    {
        private static uint WaitAfterDispose => 3;
        private static TimeSpan WaitOneDuration => TimeSpan.FromSeconds(5);
        private static IClientOptions Options;

        public ClientTestsBase(IClientOptions options = null)
        {
            Options = options;
        }
        
        [Fact]
        public void Client_Raises_OnConnected_EventArgs()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            T? client = GetClient(logger, Options);
            Assert.NotNull(client);
            try
            {
                ManualResetEvent pauseConnected = new ManualResetEvent(false);

                Assert.Raises<OnConnectedEventArgs>(
                    h => client.OnConnected += h,
                    h => client.OnConnected -= h,
                    () =>
                    {
                        client.OnConnected += (sender, e) => pauseConnected.Set();
                        client.Open();
                        Assert.True(pauseConnected.WaitOne(WaitOneDuration));
                    });
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                Cleanup(client);
            }
        }

        [Fact]
        public void Client_Raises_OnDisconnected_EventArgs()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            T? client = GetClient(logger, Options);
            Assert.NotNull(client);
            try
            {
                ManualResetEvent pauseDisconnected = new ManualResetEvent(false);

                Assert.Raises<OnDisconnectedEventArgs>(
                    h => client.OnDisconnected += h,
                    h => client.OnDisconnected -= h,
                    () =>
                    {
                        client.OnConnected += (sender, e) =>
                        {
                            Task.Delay(WaitOneDuration).GetAwaiter().GetResult();
                            client.Close();
                        };
                        client.OnDisconnected += (sender, e) => pauseDisconnected.Set();
                        client.Open();
                        Assert.True(pauseDisconnected.WaitOne(WaitOneDuration));
                    });
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                Cleanup(client);
            }
        }

        [Fact]
        public void Client_Raises_OnReconnected_EventArgs()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            T? client = GetClient(logger, Options);
            Assert.NotNull(client);
            try
            {
                ManualResetEvent pauseReconnected = new ManualResetEvent(false);

                Assert.Raises<OnConnectedEventArgs>(
                    h => client.OnReconnected += h,
                    h => client.OnReconnected -= h,
                    () =>
                    {
                        client.OnConnected += (s, e) => client.Reconnect();

                        client.OnReconnected += (s, e) => pauseReconnected.Set();
                        client.Open();

                        Assert.True(pauseReconnected.WaitOne(WaitOneDuration));
                    });
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                Cleanup(client);
            }
        }

        [Fact]
        public void Dispose_Client_Before_Connecting_IsOK()
        {
            // create one logger per test-method! - cause one file per test-method is generated
            ILogger<T> logger = TestLogHelper.GetLogger<T>();
            IClient? client = null;
            try
            {
                client = GetClient(logger, Options);
                Assert.NotNull(client);
                client.Dispose();
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
                Assert.Fail(e.ToString());
            }
            finally
            {
                Cleanup((T?)client);
            }
        }

        private static void Cleanup(T? client)
        {
            client?.Dispose();
            Task.Delay(TimeSpan.FromSeconds(WaitAfterDispose)).GetAwaiter().GetResult();
        }

        private static TClient? GetClient<TClient>(ILogger<TClient> logger, IClientOptions? options = null)
        {
            Type[] constructorParameterTypes = new Type[]
            {
                typeof(IClientOptions),
                typeof(ILogger<TClient>)
            };
            ConstructorInfo? constructor = typeof(TClient).GetConstructor(constructorParameterTypes);
            object[] constructorParameters = new object[]
            {
                options ?? new ClientOptions(),
                logger
            };
            return (TClient?)constructor?.Invoke(constructorParameters);
        }
    }
}