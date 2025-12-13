#!/bin/bash
# ================================
# WSL Unity Log Processing Script
# Reads existing client/server logs from Roaming\KyraCo
# ================================
set -e

### CONFIG ###
ROAMING_DIR="/mnt/c/Users/$USER/AppData/Roaming/KyraCo"
CLIENT_LOGS="$ROAMING_DIR/client_logs"
SERVER_LOGS="$ROAMING_DIR/server_logs"
RESULTS_DIR="results"

mkdir -p "$RESULTS_DIR"

echo "Processing logs from:"
echo "Client logs: $CLIENT_LOGS"
echo "Server logs: $SERVER_LOGS"

### POST-PROCESSING (Latency + Jitter) ###
echo "metric,mean,median,p95" > "$RESULTS_DIR/summary.csv"

python3 << EOF
import pandas as pd
import glob
import numpy as np
import os

results_dir = "$RESULTS_DIR"

# Collect all client log CSVs
client_logs_path = os.path.join("$CLIENT_LOGS", "*.csv")
latencies = []
for f in glob.glob(client_logs_path):
    df = pd.read_csv(f)
    if 'latency_ms' in df.columns:
        latencies.extend(df['latency_ms'].values)

lat = np.array(latencies)
jitter = np.abs(np.diff(lat))

def stats(arr):
    if len(arr) == 0:
        return 0,0,0
    return arr.mean(), np.median(arr), np.percentile(arr,95)

lat_m, lat_med, lat_p95 = stats(lat)
jit_m, jit_med, jit_p95 = stats(jitter)

with open(os.path.join(results_dir, "summary.csv"), "a") as f:
    f.write(f"latency_ms,{lat_m},{lat_med},{lat_p95}\n")
    f.write(f"jitter_ms,{jit_m},{jit_med},{jit_p95}\n")

# Optionally, summarize server logs if they have metrics
server_logs_path = os.path.join("$SERVER_LOGS", "*.csv")
for f in glob.glob(server_logs_path):
    df = pd.read_csv(f)
    # Add any server metrics processing here if needed
EOF

echo "Log processing complete. Summary saved in $RESULTS_DIR/summary.csv"