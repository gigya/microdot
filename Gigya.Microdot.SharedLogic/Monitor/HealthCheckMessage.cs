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

namespace Gigya.Microdot.SharedLogic.Monitor
{
    /// <summary>Represents the health state of a <see cref="HealthMessage"/></summary>
    public enum Health
    {
        // the order of this enum affects the order messages will appear (unhealthy first)

        /// <summary>Message is not healthy</summary>
        Unhealthy = 0, 
        /// <summary>Message is healthy</summary>
        Healthy = 1,
        /// <summary>Message is only an informative message, and does not indicate whether it is healthy or not</summary>
        Info = 3
    };

    /// <summary>
    /// Contains a message about the healthiness of a component
    /// </summary>
    public class HealthMessage
    {
        /// <summary>
        /// Healthiness of this message: Is it healthy or not?
        /// </summary>
        public Health Health { get; }
        
        /// <summary>
        /// Message for display
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Whether message should be suppressed and not displayed with all other messages
        /// </summary>
        public bool SuppressMessage { get; }

        public HealthMessage(Health health, string message, bool suppressMessage = false)
        {
            Health = health;
            Message = message;
            SuppressMessage = suppressMessage;
        }
    }
}
