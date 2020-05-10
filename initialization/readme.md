Identity Server - Table Storage Initialization

Follow the installation instructions.

    services.AddPersistedGrantContext(Configuration.GetSection("IdentityServerStorageConfiguration:PersistedGrantStorageConfig"))
    .CreatePersistedGrantStorage()//Can be removed after first run.
    .AddClientContext(Configuration.GetSection("IdentityServerStorageConfiguration:ClientStorageConfig"))
    .CreateClientStorage()//Can be removed after first run.
    .AddResourceContext(Configuration.GetSection("IdentityServerStorageConfiguration:ResourceStorageConfig"))
    .CreateResourceStorage() //Can be removed after first run.
    .AddDeviceFlowContext(Configuration.GetSection("IdentityServerStorageConfiguration:DeviceFlowStorageConfig"))
    .CreateDeviceFlowStorage(); //Can be removed after first run.


Add the IdentityTableStorageInitializer from this folder into your project and run the following code once.  You can remove it all afterwards.
This assumes you have setup your desired configuration in the default Identity Server Config.cs and will take the ApiResources, IdentityResources, and Clients in there and persist them to table storage to get you going.


    //Just run this one time to initialize data in storage.
    IdentityTableStorageInitializer i = new IdentityTableStorageInitializer(services);
    await i.OneTimeInitializeAsync();
