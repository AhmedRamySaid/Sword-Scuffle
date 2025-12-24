import pandas as pd
import numpy as np
import os
import sys

def compute_metrics(folder_path):
    # Load CSV files
    server_sent = pd.read_csv(os.path.join(folder_path, "server_logs.csv"))
    server_received = pd.read_csv(os.path.join(folder_path, "server_received_logs.csv"))
    player_logs = pd.read_csv(os.path.join(folder_path, "player_logs.csv"))

    # Ensure numeric types
    numeric_cols = [
        "server_timestamp",
        "client_side_x_position",
        "client_side_y_position",
        "server_side_x_position",
        "server_side_y_position"
    ]

    for df in [server_received, player_logs]:
        for col in numeric_cols:
            if col in df.columns:
                df[col] = pd.to_numeric(df[col], errors="coerce")

    # Merge player logs with server sent packets on server_timestamp
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
    merged["bandwidth_per_client_kbps"] = np.nan

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
    for metric in ["perceived_position_error"]:
        values = per_packet_df[metric].dropna()
        summary_rows.append({
            "metric": metric,
            "mean": values.mean(),
            "median": values.median(),
            "p95": np.percentile(values, 95)
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