import pandas as pd
import numpy as np
import os
import sys

def compute_metrics(folder_path, include_checksum=True):
    # Load CSV files
    server_received = pd.read_csv(os.path.join(folder_path, "server_received_logs.csv"))
    player_logs = pd.read_csv(os.path.join(folder_path, "player_logs.csv"))
    cpu_logs_path = os.path.join(folder_path, "cpu_logs.csv")

    if os.path.exists(cpu_logs_path):
        cpu_logs = pd.read_csv(cpu_logs_path)
    else:
        cpu_logs = pd.DataFrame(columns=["timestamp_ms", "cpu_percent"])

    # Ensure numeric types
    numeric_cols = [
        "server_timestamp",
        "client_side_x_position",
        "client_side_y_position",
        "server_side_x_position",
        "server_side_y_position",
        "payload_length",
        "latency"  # Include latency
    ]
    for df in [server_received, player_logs]:
        for col in numeric_cols:
            if col in df.columns:
                df[col] = pd.to_numeric(df[col], errors="coerce")

    # Compute total bytes per packet
    FIXED_HEADER_BYTES = 4 + 1 + 1 + 4 + 4 + 8 + 2
    CHECKSUM_BYTES = 4 if include_checksum else 0
    server_received['total_bytes'] = FIXED_HEADER_BYTES + CHECKSUM_BYTES + server_received['payload_length']
    server_received['total_bits'] = server_received['total_bytes'] * 8

    # Compute bandwidth per client
    bandwidth_per_client = server_received.groupby('client_ip_address').apply(
        lambda df: df['total_bits'].sum() / ((df['server_timestamp'].max() - df['server_timestamp'].min()) / 1000) / 1000
    )  # kbps

    # Merge player logs with server received packets on server_timestamp
    merged = pd.merge(
        server_received,
        player_logs,
        left_on="server_timestamp",
        right_on="server_timestamp",
        how="inner"
    )

    # Compute perceived position error
    merged["perceived_position_error"] = np.sqrt(
        (merged["server_side_x_position"] - merged["client_side_x_position"]) ** 2 +
        (merged["server_side_y_position"] - merged["client_side_y_position"]) ** 2
    )

    # Add bandwidth column
    merged["bandwidth_per_client_kbps"] = merged["client_ip_address"].map(bandwidth_per_client)

    # Per-packet metrics output (CPU removed)
    per_packet_cols = [
        "player_id",
        "snapshot_id",
        "seq_num",
        "server_timestamp",
        "perceived_position_error",
        "bandwidth_per_client_kbps"
    ]
    if "latency" in merged.columns:
        per_packet_cols.append("latency")
    per_packet_df = merged[per_packet_cols]
    per_packet_path = os.path.join(folder_path, "per_packet_metrics.csv")
    per_packet_df.to_csv(per_packet_path, index=False)

    # Summary statistics
    summary_rows = []

    # Position error stats
    pos_values = per_packet_df["perceived_position_error"].dropna()
    summary_rows.append({
        "metric": "perceived_position_error",
        "mean": pos_values.mean() if not pos_values.empty else np.nan,
        "median": pos_values.median() if not pos_values.empty else np.nan,
        "p95": np.percentile(pos_values, 95) if not pos_values.empty else np.nan
    })

    # Latency stats
    if "latency" in per_packet_df.columns:
        latency_values = per_packet_df["latency"].dropna()
        summary_rows.append({
            "metric": "latency_ms",
            "mean": latency_values.mean() if not latency_values.empty else np.nan,
            "median": latency_values.median() if not latency_values.empty else np.nan,
            "p95": np.percentile(latency_values, 95) if not latency_values.empty else np.nan
        })

    # Total bandwidth stats
    summary_rows.append({
        "metric": "total_bandwidth_kbps",
        "mean": bandwidth_per_client.mean(),
        "median": bandwidth_per_client.median(),
        "p95": np.percentile(bandwidth_per_client, 95)
    })

    # CPU usage stats
    if not cpu_logs.empty:
        cpu_values = cpu_logs["cpu_percent"].dropna()
        summary_rows.append({
            "metric": "cpu_percent",
            "mean": cpu_values.mean() if not cpu_values.empty else np.nan,
            "median": cpu_values.median() if not cpu_values.empty else np.nan,
            "p95": np.percentile(cpu_values, 95) if not cpu_values.empty else np.nan
        })

    summary_df = pd.DataFrame(summary_rows)
    summary_path = os.path.join(folder_path, "summary_statistics.csv")
    summary_df.to_csv(summary_path, index=False)

    print("Generated:")
    print(f"  {per_packet_path}")
    print(f"  {summary_path}")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python compute_metrics.py <folder_path>")
        sys.exit(1)

    compute_metrics(sys.argv[1])