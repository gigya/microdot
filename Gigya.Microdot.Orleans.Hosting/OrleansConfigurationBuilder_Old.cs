//#region Copyright 
//// Copyright 2017 Gigya Inc.  All rights reserved.
//// 
//// Licensed under the Apache License, Version 2.0 (the "License"); 
//// you may not use this file except in compliance with the License.  
//// You may obtain a copy of the License at
//// 
////     http://www.apache.org/licenses/LICENSE-2.0
//// 
//// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDER AND CONTRIBUTORS "AS IS"
//// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
//// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
//// ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
//// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
//// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
//// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
//// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
//// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
//// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
//// POSSIBILITY OF SUCH DAMAGE.
//#endregion

//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Net;
//using Gigya.Microdot.Hosting.HttpService;
//using Gigya.Microdot.Interfaces.Configuration;
//using Gigya.Microdot.Orleans.Hosting.Logging;
//using Gigya.Microdot.SharedLogic;
//using org.apache.zookeeper;
//using Orleans.Providers;
//using Orleans.Runtime;
//using Orleans.Runtime.Configuration;
//using Orleans.Storage;


//            globals.RegisterBootstrapProvider<DelegatingBootstrapProvider>(nameof(DelegatingBootstrapProvider));


//       

//     

//            // TODO: #ORLEANS20
//            //Setup Statistics
//            // var metricsProviderType = typeof(MetricsStatisticsPublisher);
//            // globals.ProviderConfigurations.Add("Statistics", new ProviderCategoryConfiguration("Statistics")
//            // {
//            //     Providers = new Dictionary<string, IProviderConfiguration>
//            //     {
//            //         {
//            //             metricsProviderType.Name,
//            //             new ProviderConfiguration(new Dictionary<string, string>(), metricsProviderType.FullName, metricsProviderType.Name)
//            //         }
//            //     }
//            // });
//            // defaults.StatisticsProviderName = metricsProviderType.Name;

//            defaults.StatisticsCollectionLevel = StatisticsLevel.Info;
//            defaults.StatisticsLogWriteInterval = TimeSpan.Parse(orleansConfig.MetricsTableWriteInterval);
//            defaults.StatisticsWriteLogStatisticsToTable = true;


//           //     globals.LivenessType = GlobalConfiguration.LivenessProviderType.MembershipTableGrain;

//                if (serviceArguments.SiloClusterMode == SiloClusterMode.PrimaryNode)
//                {
//                    globals.SeedNodes.Add(new IPEndPoint(IPAddress.Loopback, endPointDefinition.SiloNetworkingPort));
//                    SiloType = Silo.SiloType.Primary;
//                }
//                else
//                {
//                    globals.SeedNodes.Add(new IPEndPoint(IPAddress.Loopback, endPointDefinition.SiloNetworkingPortOfPrimaryNode));
//                }

//            }
//            
//          
//       

//            if (string.IsNullOrEmpty(commonConfig.StorageProviderTypeFullName) == false)
//            {
//                //globals.RegisterStorageProvider<MemoryStorage>("OrleansStorage");
//                globals.RegisterStorageProvider(commonConfig.StorageProviderTypeFullName, "Default");
//                globals.RegisterStorageProvider(commonConfig.StorageProviderTypeFullName, commonConfig.StorageProviderName);
//            }
//        }

//}