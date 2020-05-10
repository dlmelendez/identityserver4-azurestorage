# Identity Server - Table Storage Initialization

1.  Follow the installation instructions.

```c#
services.AddPersistedGrantContext(Configuration.GetSection("IdentityServer4:PersistedGrantStorageConfig"))
.CreatePersistedGrantStorage()//Can be removed after first run.
.AddClientContext(Configuration.GetSection("IdentityServer4:ClientStorageConfig"))
.CreateClientStorage()//Can be removed after first run.
.AddResourceContext(Configuration.GetSection("IdentityServer4:ResourceStorageConfig"))
.CreateResourceStorage() //Can be removed after first run.
.AddDeviceFlowContext(Configuration.GetSection("IdentityServer4:DeviceFlowStorageConfig"))
.CreateDeviceFlowStorage(); //Can be removed after first run.
```


2.  Add the `IdentityTableStorageInitializer` class from this folder into your project and run the following code once immediately after the above.  You can remove it all afterwards.
This assumes you have setup your desired configuration in the default Identity Server `Config.cs` and will take the ApiResources, IdentityResources, and Clients in there and persist them to table storage to get you going.

```c#
//Just run this one time to initialize data in storage.
IdentityTableStorageInitializer i = new IdentityTableStorageInitializer(services);
await i.OneTimeInitializeAsync();
```
