# SqliteCache for ASP.NET Core

[SqliteCache](https://neosmart.net/blog/2019/sqlite-cache-for-asp-net-core) is a persistent cache
implementing `IDistributedCache` for ASP.NET Core projects.

SqliteCache uses a locally stored SQLite database file (taking advantage of SQLite's battle-tested
safe multi-threaded access features) to replicate persistent caching, allowing developers to mimic
the behavior of staging or production targets without all the overhead or hassle of a traditional
`IDistributedCache` implementation. You can read more about its design and inspiration in [the
official release post](https://neosmart.net/blog/2019/sqlite-cache-for-asp-net-core) on the NeoSmart
blog.

## Why `NeoSmart.Caching.Sqlite`?

The currently available options for caching in ASP.NET Core projects are either all ephemeral
in-memory cache offerings (`IMemoryCache` and co.) -- aka non-persistent -- or else have a whole
slew of dependencies and requirements that require at the very least administrator privileges and
background services hogging up system resources and needing updates and maintenance to requiring
multiple machines and a persistent network configuration.

* `NeoSmart.Caching.Sqlite` has no dependencies on background services that hog system resources and
need to be updated or maintained (*cough* *cough* NCache *cough* *cough*)
* `NeoSmart.Caching.Sqlite` is fully cross-platform and runs the same on your Windows PC or your
colleagues' Linux, FreeBSD, and macOS workstations (unlike, say, Redis)
* `NeoSmart.Caching.Sqlite` doesn't need administrator privileges to install - or even any installation
for that matter (SQL Express LocalDB, this one is aimed at you)
* `NeoSmart.Caching.Sqlite` is a fully contained `IDistributedCache` offering that is installed and
updated alongside the rest of your packages via NuGet, Paket, or whatever other option you're
already using to manage your dependencies.

## Installation

SqliteCache is available via the NuGet, and can be installed in the Package Manager Console as
follows:

```
Install-Package NeoSmart.Caching.Sqlite
```

## Usage

Using SqliteCache is straight-forward, and should be extremely familiar for anyone that's configured
an ASP.NET Core application before. *Starting by adding a namespace import `using
NeoSmart.Caching.Sqlite` makes things easier as the editor will pull in the correct extension
methods.*

If using SqliteCache in an ASP.NET Core project, the SQLite-backed cache should be added as an
`IDistributedCache` type by adding the following to your `ConfigureServices` method, by default
located in `Startup.cs`:

```csharp
// using NeoSmart.Caching.Sqlite;

public void ConfigureServices(IServiceCollection services)
{
    ...

    // Note: this *must* come before services.AddMvc()!
    services.AddSqliteCache(options => {
        options.CachePath = @"C:\data\bazaar\cache.db";
    });

    services.AddMvc();

    ...
}
```

Afterwards, the `SqliteCache` instance will be made available to both the framework and the
application via dependency injection, and can be imported and used via either the
`IDistributedCache` abstract type or the concrete `SqliteCache` type:

```csharp
// using Microsoft.Extensions.Caching.Distributed;
public class FooModel(DbContext db, IDistributedCache cache)
{
    _db = db;
    _cache = cache;

    cache.SetString("foo", "bar");
    Assert.AreEqual(cache.GetString("foo"), "bar");

    Assert.AreEqual(typeof(NeoSmart.Caching.Sqlite.SqliteCache),
                    cache.GetType());
}
```

## License

SqliteCache is developed and maintained by Mahmoud Al-Qudsi of NeoSmart Technologies. The project is
provided free to the community under the terms of the MIT open source license.

## Contributing

We are open to pull requests and contributions aimed at the code, documentation, unit tests, or
anything else. If you're mulling an extensive contribution, file an issue first to make sure we're
all on the same page, otherwise, PR away!

