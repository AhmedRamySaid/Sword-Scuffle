import pandas as pd
import matplotlib.pyplot as plt
import os

def create_plots():
    client_csv = 'client_metrics.csv'
    if not os.path.exists(client_csv):
        print("client_metrics.csv not found. Run parse_logs.py first.")
        return

    df = pd.read_csv(client_csv)
    
    # We don't have recorded 'receive_time_ms' in the original logs,
    # but we can look at the server_timestamp_ms gaps for jitter.
    
    if len(df) < 2:
        print("Not enough data to plot.")
        return

    # Calculate Jitter (difference in server timestamps vs arrival gaps)
    # Since we don't have local recv_time, we'll plot the Arrival Intervals
    df['diff'] = df['server_timestamp_ms'].diff()
    
    plt.figure(figsize=(10, 6))
    plt.plot(df['snapshot_id'], df['diff'], label='Inter-packet Arrival Offset')
    plt.axhline(y=50, color='r', linestyle='--', label='Target (20Hz = 50ms)')
    plt.title('Network Jitter Analysis')
    plt.xlabel('Snapshot ID')
    plt.ylabel('ms')
    plt.legend()
    plt.grid(True)
    
    plot_file = 'network_performance.png'
    plt.savefig(plot_file)
    print(f"Plot saved as {plot_file}")

if __name__ == "__main__":
    try:
        import matplotlib
        create_plots()
    except ImportError:
        print("Matplotlib not found. Please install it using: pip install matplotlib pandas")
