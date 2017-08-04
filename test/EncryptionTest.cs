using System;
using System.Linq;
using System.Threading.Tasks;
using Shadowsocks.Encryption;
using Shadowsocks.Encryption.Stream;
using Xunit;

namespace test
{
    public class EncryptionTest
    {
        [Fact]
        public void TestMD5()
        {
            var random = new Random();
            for (int len = 1; len < 64; len++)
            {
                System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
                byte[] bytes = new byte[len];
                random.NextBytes(bytes);
                string md5str = Convert.ToBase64String(md5.ComputeHash(bytes));
                string md5str2 = Convert.ToBase64String(MbedTLS.MD5(bytes));
                Assert.True(md5str == md5str2);
            }
        }

        [Fact]
        public Task TestMbedTLSEncryption()
        {
            return RunParallelEncryptionTest(RunSingleMbedTLSEncryptionThread);
        }

        [Fact]
        public Task TestRC4Encryption()
        {
            return RunParallelEncryptionTest(RunSingleRC4EncryptionThread);
        }

        [Fact]
        public Task TestSodiumEncryption()
        {
            return RunParallelEncryptionTest(RunSingleSodiumEncryptionThread);
        }

        public async Task RunParallelEncryptionTest(Action encryptionAction)
        {
            // run it once before the multi-threading test to initialize global tables
            encryptionAction();
            await GenerateParallelRunningEncryptionTask(encryptionAction, 10);
            RNG.Close();
        }

        private Task GenerateParallelRunningEncryptionTask(Action action, int parallelNumber)
        {
            return Task.WhenAll(Enumerable.Repeat(1, parallelNumber).Select(_ => Task.Run(action)));
        }

        private void RunSingleSodiumEncryptionThread()
        {
            var method = "salsa20";
            var password = "barfool!";
            IEncryptor encryptor = new StreamSodiumEncryptor(method, password);
            IEncryptor decryptor = new StreamSodiumEncryptor(method, password);
            RunEncryptionTest(encryptor, decryptor);
        }

        private void RunSingleMbedTLSEncryptionThread()
        {
            var method = "aes-256-cfb";
            var password = "barfoo!";
            IEncryptor encryptor = new StreamMbedTLSEncryptor(method, password);
            IEncryptor decryptor = new StreamMbedTLSEncryptor(method, password);
            RunEncryptionTest(encryptor, decryptor);
        }

        private void RunSingleRC4EncryptionThread()
        {
            var method = "rc4-md5";
            var password = "barfoo!";
            IEncryptor encryptor = new StreamMbedTLSEncryptor(method, password);
            IEncryptor decryptor = new StreamMbedTLSEncryptor(method, password);
            RunEncryptionTest(encryptor, decryptor);
        }

        private void RunEncryptionTest(IEncryptor encryptor, IEncryptor decryptor)
        {
            RunEncryptionRound(encryptor, decryptor);
        }

        private void RunEncryptionRound(IEncryptor encryptor, IEncryptor decryptor)
        {
            RNG.Reload();
            byte[] plain = new byte[16384];
            byte[] cipher = new byte[plain.Length + 16];
            byte[] plain2 = new byte[plain.Length + 16];
            int outLen = 0;
            int outLen2 = 0;
            var random = new Random();
            random.NextBytes(plain);
            encryptor.Encrypt(plain, plain.Length, cipher, out outLen);
            decryptor.Decrypt(cipher, outLen, plain2, out outLen2);
            Assert.Equal(plain.Length, outLen2);
            for (int j = 0; j < plain.Length; j++)
            {
                Assert.Equal(plain[j], plain2[j]);
            }
            encryptor.Encrypt(plain, 1000, cipher, out outLen);
            decryptor.Decrypt(cipher, outLen, plain2, out outLen2);
            Assert.Equal(1000, outLen2);
            for (int j = 0; j < outLen2; j++)
            {
                Assert.Equal(plain[j], plain2[j]);
            }
        }
    }
}