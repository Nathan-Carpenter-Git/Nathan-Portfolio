document.addEventListener('DOMContentLoaded', () => {
    const dot = document.getElementById('statusDot');
    const uptimeEl = document.getElementById('statusUptime');
    const latencyEl = document.getElementById('statusLatency');
    const buildEl = document.getElementById('statusBuild');
    if (!dot) return;

    const POLL_INTERVAL_MS = 15000;

    function formatUptime(startedAtUtc) {
        const totalMinutes = Math.max(0, Math.floor((Date.now() - new Date(startedAtUtc).getTime()) / 60000));
        const days = Math.floor(totalMinutes / 1440);
        const hours = Math.floor((totalMinutes % 1440) / 60);
        const minutes = totalMinutes % 60;
        if (days > 0) return `${days}d ${hours}h`;
        if (hours > 0) return `${hours}h ${minutes}m`;
        return `${minutes}m`;
    }

    async function poll() {
        const requestStart = performance.now();
        try {
            const response = await fetch('/api/status', { cache: 'no-store' });
            if (!response.ok) throw new Error(`status ${response.status}`);
            const data = await response.json();
            const latencyMs = Math.round(performance.now() - requestStart);

            dot.classList.remove('status-down');
            dot.classList.add('status-up');
            dot.title = 'Operational';
            uptimeEl.textContent = formatUptime(data.startedAtUtc);
            latencyEl.textContent = `${latencyMs}ms`;
            buildEl.textContent = data.commit || 'dev';
        } catch {
            dot.classList.remove('status-up');
            dot.classList.add('status-down');
            dot.title = 'Unreachable';
            uptimeEl.textContent = '--';
            latencyEl.textContent = '--';
            buildEl.textContent = '--';
        }
    }

    poll();
    setInterval(poll, POLL_INTERVAL_MS);
});
