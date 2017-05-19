#region Copyright 
// Copyright 2017 Gigya Inc.  All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License.  
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.
#endregion

using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Gigya.Microdot.SharedLogic.Security
{
    internal static class CertificateHelper
    {
        public static byte[] GetHashOfRootCertificate(this X509Certificate2 certificate)
        {
            X509Chain chain = new X509Chain();
            chain.Build(certificate);
            return chain.ChainElements.Cast<X509ChainElement>().Last().Certificate.GetCertHash();
        }

        public static bool HasSameRootCertificateHash(this X509Certificate2 externalCertificate, byte[] internalRooCertHash)
        {
            X509Chain chain = new X509Chain { ChainPolicy = { RevocationMode = X509RevocationMode.NoCheck } };
            chain.Build(externalCertificate);

            return chain.HasSameRootCertificateHash(internalRooCertHash);            
        }

        public static bool HasSameRootCertificateHash(this X509Chain chain, byte[] internalRooCertHash)
        {
			byte[] externalRootHash = chain.ChainElements.Cast<X509ChainElement>().Last().Certificate.GetCertHash();
            return externalRootHash.SequenceEqual(internalRooCertHash);
        }
    }
}