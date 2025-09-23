// metrics.js - Metrics components
class MetricsManager {
    constructor() {
        this.metrics = {};
        this.updateCallbacks = {};
    }

    createMetricCard(config) {
        return `
            <div class="metric-card ${config.className || ''}" id="${config.id}">
                <div class="metric-icon bg-${config.color || 'primary'} bg-opacity-10 text-${config.color || 'primary'}">
                    <i class="${config.icon}"></i>
                </div>
                <div class="metric-value" data-metric="value">
                    ${config.value || '0'}
                </div>
                <div class="metric-label">${config.label}</div>
                ${config.showChange ? `
                    <div class="metric-change ${config.changeType || 'positive'}" data-metric="change">
                        <i class="bi bi-arrow-${config.changeType === 'negative' ? 'down' : 'up'}"></i>
                        <span>${config.changeValue || '0%'}</span>
                    </div>
                ` : ''}
                ${config.showProgress ? `
                    <div class="progress mt-2" style="height: 5px;">
                        <div class="progress-bar bg-${config.color || 'primary'}" 
                             role="progressbar" 
                             style="width: ${config.progress || 0}%">
                        </div>
                    </div>
                ` : ''}
            </div>
        `;
    }

    createMetricsGrid(metrics, containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const html = `
            <div class="row g-4">
                ${metrics.map(metric => `
                    <div class="${metric.colClass || 'col-xl-3 col-md-6'}">
                        ${this.createMetricCard(metric)}
                    </div>
                `).join('')}
            </div>
        `;

        container.innerHTML = html;
    }

    updateMetric(metricId, value, changeValue = null) {
        const card = document.getElementById(metricId);
        if (!card) return;

        const valueElement = card.querySelector('[data-metric="value"]');
        if (valueElement) {
            const oldValue = parseFloat(valueElement.textContent.replace(/[^0-9.-]/g, ''));
            const newValue = typeof value === 'number' ? value : parseFloat(value);

            // Animate value change
            this.animateValue(valueElement, oldValue, newValue, 500);
        }

        if (changeValue !== null) {
            const changeElement = card.querySelector('[data-metric="change"]');
            if (changeElement) {
                changeElement.innerHTML = `
                    <i class="bi bi-arrow-${changeValue < 0 ? 'down' : 'up'}"></i>
                    <span>${changeValue > 0 ? '+' : ''}${changeValue}%</span>
                `;
                changeElement.className = `metric-change ${changeValue < 0 ? 'negative' : 'positive'}`;
            }
        }
    }

    animateValue(element, start, end, duration) {
        const isInteger = Number.isInteger(end);
        const startTime = performance.now();

        const animate = (currentTime) => {
            const elapsed = currentTime - startTime;
            const progress = Math.min(elapsed / duration, 1);

            const value = start + (end - start) * this.easeOutCubic(progress);
            element.textContent = isInteger ?
                Math.round(value).toLocaleString() :
                value.toFixed(2).toLocaleString();

            if (progress < 1) {
                requestAnimationFrame(animate);
            }
        };

        requestAnimationFrame(animate);
    }

    easeOutCubic(t) {
        return 1 - Math.pow(1 - t, 3);
    }

    createSparkline(elementId, data, options = {}) {
        const canvas = document.getElementById(elementId);
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        const width = canvas.width;
        const height = canvas.height;

        // Clear canvas
        ctx.clearRect(0, 0, width, height);

        if (!data || data.length === 0) return;

        const max = Math.max(...data);
        const min = Math.min(...data);
        const range = max - min;
        const step = width / (data.length - 1);

        // Draw sparkline
        ctx.beginPath();
        ctx.strokeStyle = options.color || '#667eea';
        ctx.lineWidth = options.lineWidth || 2;

        data.forEach((value, index) => {
            const x = index * step;
            const y = height - ((value - min) / range) * height;

            if (index === 0) {
                ctx.moveTo(x, y);
            } else {
                ctx.lineTo(x, y);
            }
        });

        ctx.stroke();

        // Draw fill if enabled
        if (options.fill) {
            ctx.lineTo(width, height);
            ctx.lineTo(0, height);
            ctx.closePath();
            ctx.fillStyle = options.fillColor || 'rgba(102, 126, 234, 0.1)';
            ctx.fill();
        }

        // Draw points if enabled
        if (options.showPoints) {
            ctx.fillStyle = options.pointColor || '#667eea';
            data.forEach((value, index) => {
                const x = index * step;
                const y = height - ((value - min) / range) * height;
                ctx.beginPath();
                ctx.arc(x, y, 3, 0, Math.PI * 2);
                ctx.fill();
            });
        }
    }

    createGauge(elementId, value, max = 100, options = {}) {
        const container = document.getElementById(elementId);
        if (!container) return;

        const percentage = (value / max) * 100;
        const color = this.getGaugeColor(percentage, options.thresholds);

        container.innerHTML = `
            <div class="gauge-container">
                <svg viewBox="0 0 200 100" class="gauge">
                    <path d="M 10 90 A 80 80 0 0 1 190 90" 
                          fill="none" 
                          stroke="#e9ecef" 
                          stroke-width="20"/>
                    <path d="M 10 90 A 80 80 0 0 1 190 90" 
                          fill="none" 
                          stroke="${color}" 
                          stroke-width="20"
                          stroke-dasharray="${percentage * 2.51}, 251"
                          class="gauge-fill"/>
                </svg>
                <div class="gauge-value">${value}</div>
                <div class="gauge-label">${options.label || ''}</div>
            </div>
        `;
    }

    getGaugeColor(percentage, thresholds = {}) {
        if (percentage >= (thresholds.danger || 90)) return '#dc3545';
        if (percentage >= (thresholds.warning || 70)) return '#ffc107';
        return '#28a745';
    }

    startRealtimeUpdates(metricId, fetchFunction, interval = 5000) {
        if (this.updateCallbacks[metricId]) {
            clearInterval(this.updateCallbacks[metricId]);
        }

        const update = async () => {
            try {
                const value = await fetchFunction();
                this.updateMetric(metricId, value);
            } catch (error) {
                console.error(`Failed to update metric ${metricId}:`, error);
            }
        };

        update(); // Initial update
        this.updateCallbacks[metricId] = setInterval(update, interval);
    }

    stopRealtimeUpdates(metricId) {
        if (this.updateCallbacks[metricId]) {
            clearInterval(this.updateCallbacks[metricId]);
            delete this.updateCallbacks[metricId];
        }
    }

    stopAllRealtimeUpdates() {
        Object.keys(this.updateCallbacks).forEach(metricId => {
            this.stopRealtimeUpdates(metricId);
        });
    }
}

window.MetricsManager = MetricsManager;