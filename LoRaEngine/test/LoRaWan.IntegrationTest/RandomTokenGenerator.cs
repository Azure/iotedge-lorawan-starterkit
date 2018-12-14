using System;
using System.Threading.Tasks;
using System.Threading;

namespace LoRaWan.IntegrationTest
{
    internal static class RandomTokenGenerator
    {
        static SemaphoreSlim randomLock = new SemaphoreSlim(1);
        static Random random = new Random();
        internal static byte[] GetToken()
        {
            try
            {
                randomLock.Wait();
            
                byte[] token = new byte[2];
                random.NextBytes(token);
                return token;
            }
            finally
            {
                randomLock.Release();
            }
        }

        internal static async Task<byte[]> GetTokenAsync()
        {
            try
            {
                await randomLock.WaitAsync();
                byte[] token = new byte[2];
                random.NextBytes(token);
                return token;
            }
            finally
            {
                randomLock.Release();
            }
        }

    }
}