# Microdot Framework  
## An open source .NET microservices framework
   
Microdot is an open source .NET framework that answers a lot of the needs for easily creating microservices.

Some of its main features:
* **Service container** for hosting a microservice
* **Inter-service RPC** for easy interface-based service communication
* **Client-side transparent response caching** between services
* **Logging and Distributed Tracing** support
* **Client-side load balancing** and service discovery
* **Detailed health Checks** for easy service monitoring
* **Hierarchical configuration system** with online change detection
* **Dependency injection** as first-class citizen
* **Orleans integration** for creating Orleans based services and enjoying the benefits of virtual actors

Read on for an overview of the framework, and check the [wiki](https://github.com/gigya/microdot/wiki) for more details and a tutorial for building your first service.

## Details ##

The Microdot framework helps you to create scalable and reliable microservices (a ["microservice chassis"](http://microservices.io/patterns/microservice-chassis.html)), allowing you to focus on writing code that defines the logic of your service without the need to tackle the myriad of challenges of developing a distributed system. 

Microdot also plays nicely with the [Orleans](https://dotnet.github.io/orleans/) virtual actors framework, allowing you to easily write Orleans based microservices. 

Microdot implements and supports many established Microservice-related patterns. Below is a comprehensive diagram created by Chris Richardson of [Microservices.io](http://Microservices.io), with added color highlights to show which parts are implemented in Microdot (yellow), which are planned to be implemented (purple) and which patterns are not implemented but can be easily incorporated (blue).

[![Microdot supported patterns](https://cloud.githubusercontent.com/assets/1709453/26346200/20a3275c-3fae-11e7-9758-ecceec06be09.png)](http://microservices.io/patterns/microservices.html)

[Microservices.io](http://Microservices.io) contains a lot of useful and well-written information about the [microservice pattern/architecture](http://microservices.io/patterns/microservices.html) and the difficulties of implementing it correctly. If you are new to this architecture, it can help get you up to speed quickly and will likely help you utilize Microdot to its fullest.

<br/>

## Features

* A **service container** which accepts command-line parameters that define how your service runs, e.g. as a command-line process or a Windows Service, with or without console logs, the port your service will use to listen to incoming requests, whether it runs alone or as part of a cluster (and the cluster name to join), and whether it should shut down gracefully once a monitored parent PID exits. Sensible defaults are used based on your build configuration (Release/Debug).
* **inter-service RPC** allowing services to call one another. Each service exposes one or more C# interfaces, and clients call it by receiving an instance of an interface that performs transparent RPC using JSON over HTTP. This includes client-side load balancing (no need for a load balancer in front of your service), failover support, and secure comunication via HTTPS with certificates validations for sensitive services, if needed.
* Client-side, opt-in, **transparent response caching** between services. Useful to reduce end-to-end latency when many of your services rely on a few core services that serve relatively static data that is allowed to be eventually consistent. Also useful to reduce the impact of said services failing, while their responses are still cached by clients.
* **Logging and [Distributed Tracing](http://microservices.io/patterns/observability/distributed-tracing.html)** facilities to help diagnosing issues in production, such as [**Exception Tracking**](http://microservices.io/patterns/observability/exception-tracking.html). Client- and server-side events are emitted for every call and can be used to trace how a request was handled across all services (the call tree), and the latency each one contributed.
* Client-side [**Service discovery**](http://microservices.io/patterns/client-side-discovery.html) that supports [HashiCorp's Consul](https://github.com/hashicorp/consul) or manual configuration-based discovery.  
* All components emit [**performance metrics**](http://microservices.io/patterns/observability/application-metrics.html) via [Metrics.NET](https://github.com/Recognos/Metrics.NET) for real-time performance monitoring.
* Detailed [**Health Checks**](http://microservices.io/patterns/observability/health-check-api.html) are provided for each subsystem, and can easily be extended to cover your service's external dependencies.
* A hierarchical [**configuration system**](http://microservices.io/patterns/externalized-configuration.html) based on XML files which allows overriding values based on where and how the microservice is hosted (per data center, environment and microservice). The configuration is consumed from code via strongly-typed objects with automatic mapping and is refreshed at real time when XML files change.
* Highly **modular design** and first-class **dependency injection** support using [Ninject](https://github.com/ninject/Ninject), allowing you to swap out every component with your own implementation if needed.
* Tools to help test your service, for **Unit Tests**, [**Service Component Tests**](http://microservices.io/patterns/testing/service-component-test.html) and [**Service Integration Contract Test**](http://microservices.io/patterns/testing/service-integration-contract-test.html).

## Orleans integration

Microdot provides integration with **[Microsoft Orleans](https://github.com/dotnet/orleans)** which, in turn, provides:  
* **Ease of development** - A simple programming model ([Virtual Actors](https://dotnet.github.io/orleans/Documentation/Introduction.html#virtual-actors)) that relieves you from dealing with threads, locks, mutexes, transactions, distrubuted state consistency, etc.  
* **Scale up** - write async code and utilize all the power your machine has; only one thread per CPU core, cooperative multitasking and async IO. The result is high-throughput, low-latency, low-overhead services.  
* **Scale out** - Without any changes to your code, you can scale your service to any number of nodes without service interruption.  
* **Resiliency** - failure of a node only affects in-flight operations happening on that node, but your service remains operational and work is redistributed across healthy nodes. Orleans also gracefully handles situations like multiple node failure, split brain and other disasters.
* **Low latency and disk I/O** - by automatically caching your most active business entities, so they don't need to be loaded from disk when their state needs to be read.

You may choose to implement your micro-services over Orleans or not (or just some of them). In general, you're probably better off using Orleans, but in certain cases you might want not to, e.g. if you have a stateless service that requires no internal consistency or coordination -- such as an API gateway, a repository on top of a database (that handles the concurrency), pure functions such as complex calculations, image or document processing or generation, or a proxy service to external systems.

The rest of this document uses Orlean jargon such as *grains* and *silos*. It is highly recommended to familiarize yourself with those basic concepts by reading this short [introduction to Orleans](https://dotnet.github.io/orleans/docs/index.html). 

<br/>

## Getting started  
  
The easiest way to start using Microdot is by adding the `Gigya.Microdot.Orleans.Ninject.Host` NuGet to a Console Application C# project. This will also install its dependencies, which includes everything you need to build your service. Then will need to: 
  
* Create your service host that will run your service 
* Define your public-facing service interface. Methods on that interface can be called from outside. 
* Define your stateless-worker service grain and interface that implement the public-facing interface. 
* Define any other grains you need to perform the required processing. 
* Run your service (F5) 
  
A detailed step-by-step guide is available [here](https://github.com/gigya/microdot/wiki/Building-your-first-Microdot-service). 

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
