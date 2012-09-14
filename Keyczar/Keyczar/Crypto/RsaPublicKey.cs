/*  Copyright 2012 James Tuley (jay+code@tuley.name)
 * 
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Keyczar.Crypto.Streams;
using Keyczar.Util;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace Keyczar.Crypto
{
    /// <summary>
    /// The RSA public key
    /// </summary>
    public class RsaPublicKey : Key,IEncrypterKey,IVerifierkey
    {
        /// <summary>
        /// PkcsPadding identifier
        /// </summary>
        public static readonly string PkcsPadding = "PKCS";
        /// <summary>
        /// OaepPadding identifer
        /// </summary>
        public static readonly string OaepPadding = "OAEP";

        /// <summary>
        /// Gets or sets the modulus.
        /// </summary>
        /// <value>The modulus.</value>
        [JsonConverter(typeof(WebSafeBase64ByteConverter))]
        public byte[] Modulus { get; set; }
        /// <summary>
        /// Gets or sets the public exponent.
        /// </summary>
        /// <value>The public exponent.</value>
        [JsonConverter(typeof(WebSafeBase64ByteConverter))]
        public byte[] PublicExponent { get; set; }

        /// <summary>
        /// Gets or sets the padding.
        /// </summary>
        /// <value>The padding. If not set uses OEAP padding</value>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Padding { get; set; }

        /// <summary>
        /// Gets the key hash.
        /// </summary>
        /// <returns></returns>
        public override byte[] GetKeyHash()
        {      
            var magModulus = Utility.StripLeadingZeros(Modulus);
            var magPublicExponent = Utility.StripLeadingZeros(PublicExponent);
            if (Padding == PkcsPadding)
            {
                magModulus = (byte[]) Modulus.Clone();
                magPublicExponent = (byte[])PublicExponent.Clone();
            }

            var hash = Utility.HashKeyLengthPrefix(Keyczar.KEY_HASH_LENGTH, magModulus, magPublicExponent);
            magModulus.Clear();
            magPublicExponent.Clear();
            return hash;
        }

        /// <summary>
        /// Generates the key.
        /// </summary>
        /// <param name="size">The size.</param>
        protected override void GenerateKey(int size)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            Secure.Clear(Modulus);
            Modulus = null;
            Secure.Clear(PublicExponent);
            PublicExponent = null;
            Size = 0;
        }

        /// <summary>
        /// Gets the verifying stream.
        /// </summary>
        /// <returns></returns>
        public VerifyingStream GetVerifyingStream()
        {
            var signer = new RsaDigestSigner(new Sha1Digest());
            signer.Init(forSigning:false,parameters:new RsaKeyParameters(false,
                new BigInteger(Modulus), new BigInteger(PublicExponent)));
            return new DigestStream(signer);
        }

        /// <summary>
        /// Gets the length of the tag.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <returns></returns>
        public int GetTagLength(byte[] header)
        {
            return 0;
        }

        /// <summary>
        /// Updates the padding.
        /// </summary>
        /// <param name="cipher">The cipher.</param>
        /// <returns></returns>
        internal IAsymmetricBlockCipher UpdatePadding(IAsymmetricBlockCipher cipher)
        {
            if (String.IsNullOrWhiteSpace(Padding) || Padding == OaepPadding)
            {
                return new OaepEncoding(cipher, new Sha1Digest(), new Sha1Digest(), null);
            }
            if (Padding == PkcsPadding)
            {
                return new Pkcs1Encoding(cipher);
            }
            throw new InvalidKeyTypeException(string.Format("Unknown padding value: {0}!", Padding));
        }

        /// <summary>
        /// Gets the encrypting stream.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <returns></returns>
        public FinishingStream GetEncryptingStream(Stream output)
        {
            var rsa = new RsaEngine();

            var oaep =  UpdatePadding(rsa);
    
            return new AsymmetricStream(
                oaep,
                output,
                (cipher,encrypt)=>  cipher.Init(encrypt,new RsaKeyParameters(false,
                new BigInteger(Modulus), new BigInteger(PublicExponent))),
                encrypt:true);
        }

        /// <summary>
        /// Gets the authentication signing stream.
        /// </summary>
        /// <returns>null</returns>
        public HashingStream GetAuthSigningStream()
        {
            return null; //not signing
        }
    }
}