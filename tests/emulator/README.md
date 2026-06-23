# Service Bus emulator (for integration tests)

The integration suite in [`../Vibes.ASBManager.Tests.Integration`](../Vibes.ASBManager.Tests.Integration)
runs against the [Azure Service Bus emulator](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator).
It verifies the session-aware purge / dead-letter / replay behaviour that can't be exercised without a
real broker.

These tests **auto-skip** when the emulator isn't reachable on `localhost:5672`, so they're harmless in
CI and on machines without the emulator running. Bring the emulator up only when you want to actually run
them — typically before cutting a release.

## Run the tests

```bash
# 1. Start the emulator (+ its required SQL Server backend). First run pulls ~2 GB of images.
docker compose -f tests/emulator/docker-compose.yaml up -d

# 2. Wait until healthy (a few seconds; longer under emulation on Apple Silicon)
curl -fsS http://localhost:5300/health && echo " emulator ready"

# 3. Run the integration tests
dotnet test tests/Vibes.ASBManager.Tests.Integration/Vibes.ASBManager.Tests.Integration.csproj -c Release

# 4. Tear down when done
docker compose -f tests/emulator/docker-compose.yaml down
```

## Topology

`config.json` is loaded at emulator startup (entities can't be created at runtime). It defines:

| Entity | Kind | Sessions |
|--------|------|----------|
| `q-plain` | queue | no |
| `q-session` | queue | **yes** |
| `t1` / `s-plain` | topic / subscription | no |
| `t1` / `s-session` | topic / subscription | **yes** |

## Connection string

```
Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;
```

This is the emulator's fixed development connection string — it is not a secret. The SQL Server backend
password in `docker-compose.yaml` is likewise a throwaway local credential.

## Notes

- The emulator image is **amd64-only**. On Apple Silicon it runs under emulation (the `platform` keys in
  the compose file); it works but starts more slowly.
- Data plane is AMQP on **5672**; management/health is on **5300**.
