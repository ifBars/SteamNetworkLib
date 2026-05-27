# SteamNetworkLib Real-Game Behavior Report

Generated: 2026-05-27 00:38:14 -07:00

This report summarizes SteamNetworkLib behavior from a real Schedule I process harness. The current run uses two isolated game processes with a Goldberg-compatible local Steam configuration, so it is useful for message-path and chunking behavior. It is not a WAN benchmark.

## Current Status

| Metric | Value |
| --- | ---: |
| Runtime | Mono |
| Transport | Steam P2P compatible local baseline |
| Environment | Isolated Schedule I processes with Goldberg-compatible local Steam config |
| Measurements | 7 |
| Total payload | 4.09 MB |
| Messages / chunks | 97 |
| Average throughput | 146.96 Mbps |
| Slowest measurement | Small direct messages, 43 ms |
| Highest throughput | Receive a 2 MB music file, 500.81 Mbps |

~~~mermaid
xychart-beta
  title "Duration by Scenario"
  x-axis ["Model suite", "Model suite", "70 KB mod state blob", "Receive a 2 MB music file", "Share a 2 MB music file", "Direct", "Direct"]
  y-axis "Milliseconds" 0 --> 49
  bar [13.002, 27.501, 12.501, 33.500, 35.500, 32.500, 43.499]
~~~

~~~mermaid
xychart-beta
  title "Throughput by Scenario"
  x-axis ["Model suite", "Model suite", "70 KB mod state blob", "Receive a 2 MB music file", "Share a 2 MB music file", "Direct", "Direct"]
  y-axis "Mbps" 0 --> 526
  bar [6.435, 3.033, 45.873, 500.812, 472.594, 0.001, 0.001]
~~~

## Measurement Table

| Scenario | Role | Direction | Payload | Messages | Duration | Throughput |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| Built-in message model suite | client | client-to-host | 10.2 KB | 5 | 13 ms | 6.44 Mbps |
| Built-in message model suite | host | host-to-client | 10.2 KB | 5 | 28 ms | 3.03 Mbps |
| 70 KB mod state blob | host | host-to-client | 70.0 KB | 17 | 13 ms | 45.87 Mbps |
| Receive a 2 MB music file | client | host-to-client-receive | 2.00 MB | 32 | 34 ms | 500.81 Mbps |
| Share a 2 MB music file | host | host-to-client | 2.00 MB | 32 | 36 ms | 472.59 Mbps |
| Small direct messages | client | client-to-host-and-host-to-client | 3 B | 3 | 33 ms | 0.00 Mbps |
| Small direct messages | host | host-to-client-and-client-to-host | 3 B | 3 | 43 ms | 0.00 Mbps |

## Real-World Interpretation

The most useful case in this run is the 2 MB music-file transfer because it exercises SteamNetworkLib's chunked file path instead of only small direct messages. The 70 KB state blob is a smaller version of the same behavior for mod-state synchronization. The message-model suite keeps coverage on EventMessage, DataSyncMessage, HeartbeatMessage, StreamMessage, and FileTransferMessage.

Upload speed, download speed, and latency should be treated as properties of the current run's transport path. For public WAN claims, run the same metrics across separate machines or with a controlled network emulator and regenerate this report from the new JSONL.

## Standalone View

The standalone full-page report is available at [performance-report-full.html](performance-report-full.html).
