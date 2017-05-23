# Microdot Framework  
## Easily create .NET Microservices with Orleans  
   
The Microdot framework helps you to create scalable and reliable microservices (a ["microservice chassie"](http://microservices.io/patterns/microservice-chassis.html)), allowing you to focus on writing code that defines the logic of your service without the need to tackle the myriad of challenges of developing a distributed system. 

Microdot implements and supports many established Microservice-related patterns. Below is a comprehensive diagram created by Chris Richardson of [Microservices.io](http://Microservices.io), with added color highlights to show which parts are implemented in Microdot (yellow), which are planned to be implemented (purple) and which patterns are not implemented but can be easily incorporated (blue).

[![Microdot supported patterns](https://cloud.githubusercontent.com/assets/1709453/26346200/20a3275c-3fae-11e7-9758-ecceec06be09.png)](http://microservices.io/patterns/microservices.html)

[Microservices.io](http://Microservices.io) contains a lot of useful and well-written information about the [microservice pattern/architecture](http://microservices.io/patterns/microservices.html) and the difficulties of implementing it correctly. If you are new to this architecture, it can help get you up to speed quickly and will likely help you utilize Microdot to its fullest.

<br/>

## Features

Microdot builds upon **[Microsoft Orleans](https://github.com/dotnet/orleans)** which provides:  
* **Ease of development** - A simple programming model ([Virtual Actors](https://dotnet.github.io/orleans/Documentation/Introduction.html#virtual-actors)) that doesn't require you to understand threads, locks, mutexes, transactions, distrubuted state consistency, etc.  
* **Scale up** - write async code and utilize all the power your machine has; only one thread per CPU core, cooperative multitasking and async IO. The result is high-throughput, low-latency, low-overhead services.  
* **Scale out** - Without any changes to your code, you can scale your service to any number of nodes without service interruption.  
* **Resiliency** - failure of a node only affects in-flight operations happening on that node, but your service remains operational and work is redistributed across healthy nodes. Orleans also gracefully handles situations like multiple node failure, split brain and other disasters.  
   
But using Orleans by itself to build microservices is non-trivial. That's where Microdot comes in, and provides:  
* A **container for hosting Orleans** which enables it to be used in various environments (simple command-line process, native Windows Service)  
* An Orleans-style **inter-service communication layer** using JSON over HTTP, which includes client-side load balancing and failover support.  
* A hierarchical [**configuration system**](http://microservices.io/patterns/externalized-configuration.html) based on XML files which allows overriding values based on where and how the microservice is hosted (per data center, environment and microservice). The configuration is consumed from code via strongly-typed object with automatic mapping.  
* [Client-side **Service discovery**](http://microservices.io/patterns/client-side-discovery.html) that supports [HashiCorp's Consul](https://github.com/hashicorp/consul) or manual configuration-based discovery.  
* Client-side, opt-in, **transparent caching** between services.  
* First-class **dependency injection** support using [Ninject](https://github.com/ninject/Ninject).  
* **Logging and [Distributed Tracing](http://microservices.io/patterns/observability/distributed-tracing.html)** facilities to help diagnosing issues in production, such as [**Exception Tracking**](http://microservices.io/patterns/observability/exception-tracking.html). All components [**emit useful metrics**](http://microservices.io/patterns/observability/application-metrics.html) via [Metrics.NET](https://github.com/Recognos/Metrics.NET) for real-time monitoring and detailed [**Health Checks**](http://microservices.io/patterns/observability/health-check-api.html) are provided for each subsystem.
* Tools to help test your service, for **Unit Tests**, [**Service Component Tests**](http://microservices.io/patterns/testing/service-component-test.html) and [**Service Integration Contract Test**](http://microservices.io/patterns/testing/service-integration-contract-test.html).
   
Microdot also supports creating non-Orleans services, in which case the threading model, scaling and resiliency are problems the service developer must solve. It is better suited for stateless services that require no internal consistency or coordination. For example, API gateways, repository services on top of databases (where concurrency is handled by the database), pure functions such as complex calculations, image or document processing or generation, etc.

The rest of this document uses Orlean jargon such as *grains* and *silos*. It is highly recommended to familiarize yourself with those basic concepts by reading this short [introduction to Orleans](https://dotnet.github.io/orleans/Documentation/Introduction.html). 

<br/>

## Getting started  
  
The easiest way to start using Microdot is by adding the `Gigya.Microdot.Orleans.Ninject.Host` NuGet to a Console Application C# project. This will also install its dependencies, which includes everything you need to build your service. Then will need to: 
  
* Create your service host that will run your service 
* Define your public-facing service interface. Methods on that interface can be called from outside. 
* Define your stateless-worker service grain and interface that implement the public-facing interface. 
* Define any other grains you need to perform the required processing. 
* Run your service (F5) 
  
A detailed step-by-step guide is available [here](!!!). 

<br/> 

## Architecture 
  
This section details the architecture of Microdot at a high level, without going into too many details. 
  
### System Architecture 
  
![Microdot System Architecture Diagram](https://cloud.githubusercontent.com/assets/1709453/26209694/302ee1f4-3bf6-11e7-9ceb-d1aada30c9ae.png) 
  
A **service** (green) is composed of several nodes, each one is a **Microdot host** (blue) that is running an **Orleans Silo** (purple). The host accepts RPC calls via JSON over HTTP and forwards it to the Silo. Calls to the host can come from clients (yellow), e.g. frontend, or from other services. Each Orleans Silo is part of an Orleans cluster, and every Silo communicate with other silos in the cluster using a propriatary binary communication protocol. Each Silo in an Orleans cluster also connects to a Membership Table (e.g. ZooKeeper, Consul, Azure or other high-availability database), which it uses to discover other Silos in the same cluster (not shown in diagram).
  
<br/>
  
### Node Architecture 
  
![Microdot Node Architecture Diagram](https://cloud.githubusercontent.com/assets/1709453/26209772/61e36c88-3bf6-11e7-90f7-dd839f7eea4f.png) 
  
Each node is composed of a Microdot host which contains three main components: **HttpServiceListener** (dark blue), **Service Grain** (orange) and **other grains** (white).  
  
* HttpServiceListener is responsible for listening to incoming HTTP requests, parsing them and calling the appropriate method on the Service Grain. 
* The Service Grain is responsible for exposing and handling the public API of your service. It will receive calls from the HttpServiceListener and needs to do initial processing on them and usually dispatch them to other grains. It is required by the Microdot framework, and is treated as the entry point of your service. 
* All the other grains are responsible for the core functions of your service. Most, if not all of your service's logic will reside in those grains. The methods on these grains are not exposed via by Microdot and can only be called from within that service (except when using an Orleans feature, 'Outside Grain Client', in which case it is possible to call any grain directly using Orlean's binary communication protocol, but this can be blocked if desired). 
* The Service Interface, an ordinary .NET interface that defines the public-facing API of your service, is published via NuGet (usually an internal server) so that other client can call your service (e.g. other services, frontend/GUI, DevOps tools, etc). 
* The Service Interface NuGet is used by client in conjunction with the ServiceProxy, which generates (at runtime) a client that implements that interface. Any calls to the client are transformed into JSON that contains which method was called, the arguments that were passed to the method and additional tracing data. The JSON is sent to the service over HTTP (the hostname, port and protocol are resolved using Service Discovery) and the remote service returns a JSON representing the return value of the method, which is deserialized and returned to the caller of the ServiceProxy. 
  
<br/><br/> 


### Class Diagram 
(should be moved to step-by-step guide) 
  
![AccountingService Class Diagram](https://cloud.githubusercontent.com/assets/1709453/26209155/b66ea166-3bf4-11e7-8a4b-621d600d676b.png) 
  
  
  
  
  
 
