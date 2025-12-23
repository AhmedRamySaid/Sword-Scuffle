#!/bin/bash
# ==============================================================================
# run_all_tests.sh
# Automates the application of network impairment scenarios using tc/netem.
# Run this script inside WSL2.
# ==============================================================================

# 0. Get Script Directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Results should be relative to where the script is run (usually project root)
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# 1. Detect Interface
INTERFACE=$(ip route get 8.8.8.8 | awk '{print $5; exit}')
if [ -z "$INTERFACE" ]; then
    INTERFACE="eth0"
fi

DURATION=60 # Increased to 60s so you have time to play
echo "Using interface: $INTERFACE"

# Function to clear Unity logs from WSL
function clear_appdata_logs() {
    local base_log_path="/mnt/c/Users/nouuu/AppData/LocalLow/KyraCo/Sword Scuffle"
    if [ -d "$base_log_path" ]; then
        echo "Clearing old logs for a clean scenario..."
        rm -f "$base_log_path/server_logs.txt" 2>/dev/null
        rm -f "$base_log_path/client_logs.txt" 2>/dev/null
        rm -f "$base_log_path/server_metrics.csv" 2>/dev/null
        rm -f "$base_log_path/client_metrics.csv" 2>/dev/null
    fi
}

# Clean existing rules
sudo tc qdisc del dev "$INTERFACE" root 2>/dev/null

function apply_and_wait() {
    local scenario_name=$1
    local netem_params=$2

    echo "===================================================="
    echo "SCENARIO: $scenario_name"
    echo "APPLYING: $netem_params"
    echo "===================================================="

    # 1. Clear logs first so this scenario's data is clean
    clear_appdata_logs

    # 2. Apply impairment
    if [ "$netem_params" != "none" ]; then
        sudo tc qdisc add dev "$INTERFACE" root netem $netem_params
    fi

    echo "Collecting data for $DURATION seconds..."
    echo ">>> ACTION: Keep your Host & Clients running and PLAY now!"
    
    # Countdown
    for i in $(seq $DURATION -1 1); do
        echo -ne "Time remaining: $i s  \r"
        sleep 1
    done
    echo -e "\nScenario complete."

    # 3. Clear impairment
    sudo tc qdisc del dev "$INTERFACE" root 2>/dev/null
    echo "Network cleared."
    
    # 4. IMPORTANT: Parse logs into the results folder for this scenario
    python3 "$SCRIPT_DIR/collect_metrics.py" "$scenario_name"
    echo ""
}

# Ensure results folder is clean for the new run
# Results are saved in the CURRENT directory where the script is launched
rm -rf results
mkdir -p results

# Scenario 1: Baseline (No Impairment)
apply_and_wait "baseline" "none"

# Scenario 2: Loss 2% (LAN-like)
apply_and_wait "loss_2" "loss 2%"

# Scenario 3: Loss 5% (WAN-like)
apply_and_wait "loss_5" "loss 5%"

# Scenario 4: Delay 100ms (WAN delay)
apply_and_wait "delay_100ms" "delay 100ms"

echo "All automated network scenarios have been executed."
echo "Running final comparison and plotting..."
python3 "$SCRIPT_DIR/process_results.py"

echo "Phase 2 Complete. Check the 'results' folder for plots and CSV summaries."
