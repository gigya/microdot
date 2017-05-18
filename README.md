# Microdot Framework 
## Easily create .NET Microservices with Orleans 
  
The Microdot framework helps you to create scalable and reliable microservices, allowing you to focus on writing code that defines the logic of your service without the need to tackle the myriad of challenges of developing a distributed system. 
  
Microdot builds upon **[Microsoft Orleans](https://github.com/dotnet/orleans)** which provides: 
* **Ease of development** - A simple programming model (Virtual Actors) that doesn't require you to understand threads, locks, mutexes, transactions, cache coherency, etc. 
* **Scale up** - write async code and utilize all the power your machine has; only one thread per CPU core, cooperative multitasking and async IO. The result is high-throughput, low-latency, low-overhead services. 
* **Scaling out** - Without any changes to your code, you can scale your service to any number of nodes and make changes at runtime. 
* **Resiliency** - failure of a node only affects in-flight operations happening on that node, but your service remains operational and work is redistributed across healthy nodes. Orleans also handles situations like multiple node failure, split brain and other disasters. 
  
But using Orleans by itself to build microservices is non-trivial. That's where Microdot comes in, and provides: 
* A **container for hosting Orleans** which enables it to be used in various environments (simple command-line process, native Windows Service) 
* An Orleans-style **inter-service communication layer** using JSON over HTTP, which includes client-side load balancing and failover support. 
* A hierarchical **configuration system** based on XML files which allows overriding values based on where and how the microservice is hosted (per data center, environment and microservice). The configuration is consumed from code via strongly-typed object with automatic mapping. 
* **Service discovery** that support [HashiCorp's Consul](https://github.com/hashicorp/consul) or manual configuration-based discovery. 
* Client-side, opt-in, **transparent caching** between services. 
* First-class **dependency injection** support using [Ninject](https://github.com/ninject/Ninject). 
* **Logging and tracing** facilities to help diagnosing issues in production. Also, all components emit useful statistics via [Metrics.NET](https://github.com/Recognos/Metrics.NET) for real-time monitoring. 
* Tools to help test your service, for both **unit tests** and acceptance tests. 
  
Microdot also supports creating non-Orleans services, in which case the threading model, scaling and resiliency are problems the service developer must solve. 
  
  
## Getting started 
  
The easiest way to start using Microdot is by adding the `Gigya.Microdot.Orleans.Ninject.Host` NuGet to a Console Application C# project. This will also install its dependencies, which includes everything you need to build your service. Then will need to:

* Create your service host that will run your service
* Define your public-facing service interface. Methods on that interface can be called from outside.
* Define your stateless-worker service grain and interface that implement the public-facing interface.
* Define any other grains you need to perform the required processing.
* Press F5

A detailed step-by-step guide is available here.

## Architecture

This section details the architecture of Microdot at a high level, without going into too many details.

### System Architecture

![Microdot System Architecture Diagram](https://cloud.githubusercontent.com/assets/1709453/26209694/302ee1f4-3bf6-11e7-9ceb-d1aada30c9ae.png)

A <span style="color:MediumSeaGreen">**service**</span> is composed of several nodes, each one is a <span style="color:LightSkyBlue">**Microdot host**</span> that is running an <span style="color:Plum">**Orleans Silo**</span>. The host accepts RPC calls via JSON over HTTP and forwards it to the <span style="color:Plum">**Silo**</span>. Calls to the <span style="color:LightSkyBlue">**host**</span> can come from <span style="color:Gold">**clients**</span> (e.g. frontend) or from other servies. Each <span style="color:Plum">**Orleans Silo**</span> is part of an Orleans cluster, and they communicate between each other using a propriatary binary communication protocol. Outside the developer's machine, each <span style="color:Plum">**Silo**</span> in an Orleans cluster also connects to a Membership Table (e.g. ZooKeeper, Consul or other high-availability database), which it uses to discover other Silos in the same cluster (not shown in diagram).

<br/><br/>

### Node Architecture

![Microdot Node Architecture Diagram](https://cloud.githubusercontent.com/assets/1709453/26209772/61e36c88-3bf6-11e7-90f7-dd839f7eea4f.png)

Each node is composed of the Microdot host which contains the 


<br/><br/>

### Class Diagram

![AccountingService Class Diagram](https://cloud.githubusercontent.com/assets/1709453/26209155/b66ea166-3bf4-11e7-8a4b-621d600d676b.png)
