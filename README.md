# SqliteCache for ASP.NET Core

SqliteCache is a persistent cache implementing `IDistributedCache` for ASP.NET Core projects.

SqliteCache uses a locally stored SQLite database file (taking advantage of SQLite's battle-tested
safe multi-threaded access features) to replicate persistent caching, allowing developers to mimic
the behavior of staging or production targets without all the overhead or hassle of a traditional
`IDistributedCache` implementation.

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

## License

SqliteCache is developed and maintained by Mahmoud Al-Qudsi of NeoSmart Technologies. The project is
provided free to the community under the terms of the MIT open source license.

## Contributing

We are open to pull requests and contributions aimed at the code, documentation, unit tests, or
anything else. If you're mulling an extensive contribution, file an issue first to make sure we're
all on the same page, otherwise, PR away!

