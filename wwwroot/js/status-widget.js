document.addEventListener('DOMContentLoaded', () => {
    const dot = document.getElementById('statusDot');
    const latencyEl = document.getElementById('statusLatency');
    const buildEl = document.getElementById('statusBuild');
    if (!dot) return;

    const POLL_INTERVAL_MS = 15000;

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
            latencyEl.textContent = `${latencyMs}ms`;
            buildEl.textContent = data.commit || 'dev';
        } catch {
            dot.classList.remove('status-up');
            dot.classList.add('status-down');
            dot.title = 'Unreachable';
            latencyEl.textContent = '--';
            buildEl.textContent = '--';
        }
    }

    let intervalId = null;

    function startPolling() {
        if (intervalId !== null) return;
        poll();
        intervalId = setInterval(poll, POLL_INTERVAL_MS);
    }

    function stopPolling() {
        if (intervalId === null) return;
        clearInterval(intervalId);
        intervalId = null;
    }

    document.addEventListener('visibilitychange', () => {
        if (document.hidden) {
            stopPolling();
        } else {
            startPolling();
        }
    });

    if (!document.hidden) startPolling();
});
