import pandas as pd
import numpy as np
import os
import sys

def compute_metrics(folder_path, include_checksum=True):
    # Load CSV files
    server_received = pd.read_csv(os.path.join(folder_path, "server_received_logs.csv"))
    player_logs = pd.read_csv(os.path.join(folder_path, "player_logs.csv"))

    # Ensure numeric types
    numeric_cols = [
        "server_timestamp",
        "client_side_x_position",
        "client_side_y_position",
        "server_side_x_position",
        "server_side_y_position",
        "payload_length"
    ]

    for df in [server_received, player_logs]:
        for col in numeric_cols:
            if col in df.columns:
                df[col] = pd.to_numeric(df[col], errors="coerce")

    # Compute total bytes per packet
    FIXED_HEADER_BYTES = 4 + 1 + 1 + 4 + 4 + 8 + 2  # sum of all fixed-size fields
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

    # Optional placeholders if not instrumented
    merged["cpu_percent"] = np.nan

    # Add bandwidth column for merged rows
    merged["bandwidth_per_client_kbps"] = merged["client_ip_address"].map(bandwidth_per_client)

    # Per-packet metrics output
    per_packet_cols = [
        "player_id",
        "snapshot_id",
        "seq_num",
        "server_timestamp",
        "perceived_position_error",
        "cpu_percent",
        "bandwidth_per_client_kbps"
    ]

    per_packet_df = merged[per_packet_cols]
    per_packet_path = os.path.join(folder_path, "per_packet_metrics.csv")
    per_packet_df.to_csv(per_packet_path, index=False)

    # Summary statistics
    summary_rows = []
    # Position error stats
    values = per_packet_df["perceived_position_error"].dropna()
    summary_rows.append({
        "metric": "perceived_position_error",
        "mean": values.mean() if not values.empty else np.nan,
        "median": values.median() if not values.empty else np.nan,
        "p95": np.percentile(values, 95) if not values.empty else np.nan
    })
    # Total bandwidth stats (sum across all clients)
    total_bandwidth_kbps = bandwidth_per_client.sum()
    summary_rows.append({
        "metric": "total_bandwidth_kbps",
        "mean": bandwidth_per_client.mean(),  # mean across clients
        "median": bandwidth_per_client.median(),  # median across clients
        "p95": np.percentile(bandwidth_per_client, 95)  # 95th percentile across clients
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