// ReSharper disable StringLiteralTypo

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Passport;
using Telegram.Bot.Types.Passport;
using Xunit;

namespace Telegram.Bot.Tests.Unit.Passport.Decryption
{
    /// <summary>
    /// Tests for decrypting file streams using <see cref="IDecrypter.DecryptFileAsync"/> method
    /// </summary>
    public class FileStreamDecryptionTests : IClassFixture<FileStreamDecryptionTests.Fixture>
    {
        public class Fixture
        {
            public Fixture() =>
                FixtureHelpers.CopyTestFiles(
                    ("driver_license-selfie.jpg.enc", "s_dec1.driver_license-selfie.jpg.enc"),
                    ("driver_license-selfie.jpg.enc", "s_dec2.driver_license-selfie.jpg.enc"),
                    ("driver_license-selfie.jpg", "s_dec3.driver_license-selfie.jpg"),
                    ("driver_license-selfie.jpg.enc", "s_dec3.driver_license-selfie.jpg.enc"),
                    ("driver_license-selfie.jpg.enc", "s_dec4.driver_license-selfie.jpg.enc"),
                    ("driver_license-selfie.jpg.enc", "s_dec5.driver_license-selfie.jpg.enc"),
                    ("driver_license-selfie.jpg", "s_dec6.driver_license-selfie.jpg"),
                    ("driver_license-selfie.jpg", "s_dec7.driver_license-selfie.jpg")
                );
        }

        [Fact(DisplayName = "Should decrypt from a seekable stream")]
        public async Task Should_Decrypt_From_Seekable_Stream()
        {
            FileCredentials fileCredentials = new FileCredentials
            {
                FileHash = "v3q47iscI6TS94CMo7HGQUOxw28LIf82NJBkImzP57c=",
                Secret = "vF7nut7clg/H/pEaTJigo4mQJ0s8B+HGCWKTWtOTIdo=",
            };

            IDecrypter decrypter = new Decrypter();

            await using Stream encContentStream = new MemoryStream();
            await using (Stream encFileStream = new NonSeekableFileReadStream(
                "Files/Passport/s_dec1.driver_license-selfie.jpg.enc"))
            {
                await encFileStream.CopyToAsync(encContentStream);
            }

            await using Stream contentStream = new MemoryStream();

            encContentStream.Position = 0; // Ensure method starts reading the content fro the beginning

            await decrypter.DecryptFileAsync(
                encContentStream,
                fileCredentials,
                contentStream
            );
        }

        [Fact(DisplayName = "Should decrypt from non-seekable stream")]
        public async Task Should_Decrypt_From_NonSeekable_Stream()
        {
            FileCredentials fileCredentials = new FileCredentials
            {
                FileHash = "v3q47iscI6TS94CMo7HGQUOxw28LIf82NJBkImzP57c=",
                Secret = "vF7nut7clg/H/pEaTJigo4mQJ0s8B+HGCWKTWtOTIdo=",
            };

            IDecrypter decrypter = new Decrypter();

            await using Stream encFileStream = new NonSeekableFileReadStream(
                "Files/Passport/s_dec2.driver_license-selfie.jpg.enc"
            );
            await using Stream fileStream = new MemoryStream();

            await decrypter.DecryptFileAsync(
                encFileStream,
                fileCredentials,
                fileStream
            );
        }

        [Fact(DisplayName = "Should throw when trying to decrypt an unencrypted file")]
        public async Task Should_Throw_Decrypting_Unencrypted_File()
        {
            FileCredentials fileCredentials = new FileCredentials
            {
                FileHash = "v3q47iscI6TS94CMo7HGQUOxw28LIf82NJBkImzP57c=",
                Secret = "vF7nut7clg/H/pEaTJigo4mQJ0s8B+HGCWKTWtOTIdo=",
            };

            IDecrypter decrypter = new Decrypter();

            await using Stream encFileStream = new NonSeekableFileReadStream(
                "Files/Passport/s_dec3.driver_license-selfie.jpg",
                throwOnFlush: false
            );
            await using Stream fileStream = new MemoryStream();

            Exception exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
                await decrypter.DecryptFileAsync(
                    encFileStream,
                    fileCredentials,
                    fileStream
                )
            );

            Assert.Equal("The input data is not a complete block.", exception.Message);
        }

        [Fact(DisplayName = "Should throw when decrypting from a stream that is not positioned at the beginning")]
        public async Task Should_Throw_Decrypting_Mispositioned_Stream()
        {
            FileCredentials fileCredentials = new FileCredentials
            {
                FileHash = "v3q47iscI6TS94CMo7HGQUOxw28LIf82NJBkImzP57c=",
                Secret = "vF7nut7clg/H/pEaTJigo4mQJ0s8B+HGCWKTWtOTIdo=",
            };

            IDecrypter decrypter = new Decrypter();

            await using Stream encContentStream = new MemoryStream();
            await using (Stream encFileStream = new NonSeekableFileReadStream(
                "Files/Passport/s_dec4.driver_license-selfie.jpg.enc"))
            {
                await encFileStream.CopyToAsync(encContentStream);
            }

            // encContentStream position is at the end and not at 0 position
            await using Stream contentStream = new MemoryStream();
            Exception exception = await Assert.ThrowsAsync<PassportDataDecryptionException>(
                async () => await decrypter.DecryptFileAsync(
                    encContentStream,
                    fileCredentials,
                    contentStream
                )
            );

            Assert.Matches(@"^Data padding length is invalid: \d+\.", exception.Message);
        }

        [Fact(DisplayName = "Should throw when decrypting a file(selfie) using wrong file credentials(front side)")]
        public async Task Should_Throw_Decrypting_Stream_With_Wrong_FileCredentials()
        {
            FileCredentials wrongFileCredentials = new FileCredentials
            {
                FileHash = "THTjgv2FU7kff/29Vty/IcqKPmOGkL7F35fAzmkfZdI=",
                Secret = "a+jxJoKPEaz77VCjRvDVcYHfIO3+h+oI+ruZh+KkYa0=",
            };

            IDecrypter decrypter = new Decrypter();

            await using Stream encFileStream = new NonSeekableFileReadStream(
                "Files/Passport/s_dec5.driver_license-selfie.jpg.enc"
            );
            await using Stream fileStream = new MemoryStream();

            Exception exception = await Assert.ThrowsAsync<PassportDataDecryptionException>(
                async () => await decrypter.DecryptFileAsync(
                    encFileStream,
                    wrongFileCredentials,
                    fileStream
                )
            );

            Assert.Matches(@"^Data hash mismatch at position \d+\.$", exception.Message);
        }

        [Fact(DisplayName = "Should throw when null data stream is passed")]
        public async Task Should_Throw_If_Null_EncryptedContent()
        {
            IDecrypter decrypter = new Decrypter();

            Exception exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await decrypter.DecryptFileAsync(null!, null!, null!)
            );

            Assert.Matches(
                @"^Value cannot be null\.\s+\(Parameter 'encryptedContent'\)$",
                exception.Message
            );
        }

        [Fact(DisplayName = "Should throw when null file credentials is passed")]
        public async Task Should_Throw_If_Null_FileCredentials()
        {
            IDecrypter decrypter = new Decrypter();

            Exception exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                decrypter.DecryptFileAsync(new MemoryStream(), null!, null!)
            );

            Assert.Matches(
                @"^Value cannot be null\.\s+\(Parameter 'fileCredentials'\)$",
                exception.Message
            );
        }

        [Fact(DisplayName = "Should throw when null secret is passed")]
        public async Task Should_Throw_If_Null_Secret()
        {
            IDecrypter decrypter = new Decrypter();
            Exception exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await decrypter.DecryptFileAsync(
                    new MemoryStream(),
                    new FileCredentials(),
                    new MemoryStream()
                )
            );

            Assert.Matches(
                @"^Value cannot be null\.\s+\(Parameter 'Secret'\)$",
                exception.Message
            );
        }

        [Fact(DisplayName = "Should throw when null file_hash is passed")]
        public async Task Should_Throw_If_Null_Hash()
        {
            IDecrypter decrypter = new Decrypter();

            FileCredentials fileCredentials = new FileCredentials {Secret = ""};
            Exception exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await decrypter.DecryptFileAsync(
                    new MemoryStream(),
                    fileCredentials,
                    new MemoryStream()
                )
            );

            Assert.Matches(
                @"^Value cannot be null\.\s+\(Parameter 'FileHash'\)$",
                exception.Message
            );
            Assert.IsType<ArgumentNullException>(exception);
        }

        [Fact(DisplayName = "Should throw when non-readable data stream is passed")]
        public async Task Should_Throw_If_NonReadable_Data_Stream()
        {
            IDecrypter decrypter = new Decrypter();
            FileCredentials fileCredentials = new FileCredentials {Secret = "", FileHash = ""};

            await using Stream encStream = File.OpenWrite(
                "Files/Passport/s_dec6.driver_license-selfie.jpg"
            );

            Exception exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await decrypter.DecryptFileAsync(encStream, fileCredentials, new MemoryStream())
            );

            Assert.Matches(
                @"^Stream does not support reading\.\s+\(Parameter 'encryptedContent'\)$",
                exception.Message
            );
        }

        [Fact(DisplayName = "Should throw when seekable data stream is empty")]
        public async Task Should_Throw_If_Empty_Data_Stream()
        {
            FileCredentials fileCredentials = new FileCredentials {Secret = "", FileHash = ""};
            IDecrypter decrypter = new Decrypter();

            Exception exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await decrypter.DecryptFileAsync(
                    new MemoryStream(),
                    fileCredentials,
                    new MemoryStream()
                )
            );

            Assert.Matches(
                @"^Stream is empty\.\s+\(Parameter 'encryptedContent'\)$",
                exception.Message
            );
        }

        [Fact(DisplayName = "Should throw when seekable data stream has invalid length")]
        public async Task Should_Throw_If_Invalid_Seekable_Data_Stream_Length()
        {
            IDecrypter decrypter = new Decrypter();
            FileCredentials fileCredentials = new FileCredentials {Secret = "", FileHash = ""};

            await using Stream encStream = new MemoryStream(new byte[16 - 1]);
            Exception exception = await Assert.ThrowsAsync<PassportDataDecryptionException>(
                async () => await decrypter.DecryptFileAsync(
                    encStream,
                    fileCredentials,
                    new MemoryStream()
                )
            );

            Assert.Equal("Data length is not divisible by 16: 15.", exception.Message);
        }

        [Fact(DisplayName = "Should throw when null destination stream is passed")]
        public async Task Should_Throw_If_Null_Destination()
        {
            IDecrypter decrypter = new Decrypter();
            FileCredentials fileCredentials = new FileCredentials {Secret = "", FileHash = ""};

            Exception exception = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await decrypter.DecryptFileAsync(new MemoryStream(), fileCredentials, null!)
            );

            Assert.Matches(
                @"^Value cannot be null\.\s+\(Parameter 'destination'\)$",
                exception.Message
            );
        }

        [Fact(DisplayName = "Should throw when destination data stream is not writable")]
        public async Task Should_Throw_If_NonWritable_Destination()
        {
            IDecrypter decrypter = new Decrypter();
            FileCredentials fileCredentials = new FileCredentials
            {
                Secret = "",
                FileHash = ""
            };

            await using Stream destStream = File.OpenRead(
                "Files/Passport/s_dec7.driver_license-selfie.jpg"
            );

            Exception exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
                await decrypter.DecryptFileAsync(
                    new MemoryStream(new byte[16]),
                    fileCredentials,
                    destStream
                )
            );

            Assert.Matches(
                @"^Stream does not support writing\.\s+\(Parameter 'destination'\)$",
                exception.Message
            );
        }

        [Theory(DisplayName = "Should throw when secret is not a valid base64-encoded string")]
        [InlineData("foo")]
        [InlineData("FooBarBazlg/H/pEaTJigo4mQJ0s8B+HGCWKTWtOTIdo=")]
        public async Task Should_Throw_If_Invalid_Secret(string secret)
        {
            FileCredentials fileCredentials = new FileCredentials
            {
                Secret = secret,
                FileHash = ""
            };
            IDecrypter decrypter = new Decrypter();

            await Assert.ThrowsAsync<FormatException>(async () =>
                await decrypter.DecryptFileAsync(
                    new MemoryStream(new byte[16]),
                    fileCredentials,
                    new MemoryStream()
                )
            );
        }

        [Theory(DisplayName = "Should throw when file_hash is not a valid base64-encoded string")]
        [InlineData("foo")]
        [InlineData("FooBarBazlg/H/pEaTJigo4mQJ0s8B+HGCWKTWtOTIdo=")]
        public async Task Should_Throw_If_Invalid_Hash(string fileHash)
        {
            FileCredentials fileCredentials = new FileCredentials
            {
                Secret = "",
                FileHash = fileHash
            };
            IDecrypter decrypter = new Decrypter();

            await Assert.ThrowsAnyAsync<FormatException>(async () =>
                await decrypter.DecryptFileAsync(
                    new MemoryStream(new byte[16]),
                    fileCredentials,
                    new MemoryStream()
                )
            );
        }

        [Theory(DisplayName = "Should throw when length of file_hash is not 32")]
        [InlineData("")]
        [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==")]
        [InlineData("Zm9v")]
        public async Task Should_Throw_If_Invalid_Hash_Length(string fileHash)
        {
            FileCredentials fileCredentials = new FileCredentials
            {
                Secret = "",
                FileHash = fileHash
            };
            IDecrypter decrypter = new Decrypter();

            Exception exception = await Assert.ThrowsAsync<PassportDataDecryptionException>(
                async () => await decrypter.DecryptFileAsync(
                    new MemoryStream(new byte[16]),
                    fileCredentials,
                    new MemoryStream()
                )
            );

            Assert.Matches(@"^Hash length is not 32: \d+\.$", exception.Message);
        }

        class NonSeekableFileReadStream : Stream
        {
            private readonly bool _throwOnFlush;
            private readonly Stream _fileStream;

            public NonSeekableFileReadStream(string path, bool throwOnFlush = true)
            {
                _throwOnFlush = throwOnFlush;
                _fileStream = File.OpenRead(path);
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                _fileStream.Read(buffer, offset, count);

            public override async Task<int> ReadAsync(
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken) =>
                await _fileStream.ReadAsync(buffer, offset, count, cancellationToken);

            public override bool CanRead => _fileStream.CanRead;

            protected override void Dispose(bool disposing)
            {
                _fileStream.Dispose();
                base.Dispose(disposing);
            }

            public override void Flush()
            {
                if (_throwOnFlush) throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();

            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }
        }
    }
}