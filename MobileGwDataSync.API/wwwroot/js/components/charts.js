// charts.js - Chart components
class ChartManager {
    constructor() {
        this.charts = {};
        this.defaultOptions = {
            responsive: true,
            plugins: {
                legend: {
                    position: 'bottom',
                },
                tooltip: {
                    mode: 'index',
                    intersect: false,
                }
            }
        };
    }

    createActivityChart(elementId, data) {
        const ctx = document.getElementById(elementId)?.getContext('2d');
        if (!ctx) return null;

        // Destroy existing chart if exists
        if (this.charts[elementId]) {
            this.charts[elementId].destroy();
        }

        this.charts[elementId] = new Chart(ctx, {
            type: 'line',
            data: {
                labels: data.labels,
                datasets: [{
                    label: 'Successful',
                    data: data.successful,
                    borderColor: 'rgb(40, 167, 69)',
                    backgroundColor: 'rgba(40, 167, 69, 0.1)',
                    tension: 0.4,
                    fill: true
                }, {
                    label: 'Failed',
                    data: data.failed,
                    borderColor: 'rgb(220, 53, 69)',
                    backgroundColor: 'rgba(220, 53, 69, 0.1)',
                    tension: 0.4,
                    fill: true
                }]
            },
            options: {
                ...this.defaultOptions,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            stepSize: 1,
                            precision: 0
                        }
                    }
                }
            }
        });

        return this.charts[elementId];
    }

    createStatusChart(elementId, data) {
        const ctx = document.getElementById(elementId)?.getContext('2d');
        if (!ctx) return null;

        if (this.charts[elementId]) {
            this.charts[elementId].destroy();
        }

        this.charts[elementId] = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: data.labels,
                datasets: [{
                    data: data.values,
                    backgroundColor: [
                        'rgba(40, 167, 69, 0.8)',
                        'rgba(220, 53, 69, 0.8)',
                        'rgba(255, 193, 7, 0.8)',
                        'rgba(108, 117, 125, 0.8)'
                    ],
                    borderWidth: 2,
                    borderColor: '#fff'
                }]
            },
            options: {
                ...this.defaultOptions,
                maintainAspectRatio: true,
                plugins: {
                    ...this.defaultOptions.plugins,
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 15,
                            usePointStyle: true
                        }
                    }
                }
            }
        });

        return this.charts[elementId];
    }

    createBarChart(elementId, data) {
        const ctx = document.getElementById(elementId)?.getContext('2d');
        if (!ctx) return null;

        if (this.charts[elementId]) {
            this.charts[elementId].destroy();
        }

        this.charts[elementId] = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: data.labels,
                datasets: data.datasets.map(dataset => ({
                    ...dataset,
                    borderRadius: 5,
                    barThickness: 40
                }))
            },
            options: {
                ...this.defaultOptions,
                maintainAspectRatio: false,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });

        return this.charts[elementId];
    }

    createRealtimeChart(elementId, maxDataPoints = 20) {
        const ctx = document.getElementById(elementId)?.getContext('2d');
        if (!ctx) return null;

        if (this.charts[elementId]) {
            this.charts[elementId].destroy();
        }

        this.charts[elementId] = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [{
                    label: 'Records/sec',
                    data: [],
                    borderColor: 'rgb(75, 192, 192)',
                    backgroundColor: 'rgba(75, 192, 192, 0.1)',
                    tension: 0.4,
                    fill: true
                }]
            },
            options: {
                ...this.defaultOptions,
                maintainAspectRatio: false,
                scales: {
                    x: {
                        display: true
                    },
                    y: {
                        beginAtZero: true
                    }
                },
                animation: {
                    duration: 0
                }
            }
        });

        // Method to add data point
        this.charts[elementId].addData = (label, value) => {
            const chart = this.charts[elementId];
            chart.data.labels.push(label);
            chart.data.datasets[0].data.push(value);

            // Remove old data points if exceeded max
            if (chart.data.labels.length > maxDataPoints) {
                chart.data.labels.shift();
                chart.data.datasets[0].data.shift();
            }

            chart.update('none'); // Update without animation
        };

        return this.charts[elementId];
    }

    updateChart(elementId, data) {
        const chart = this.charts[elementId];
        if (!chart) return;

        if (data.labels) chart.data.labels = data.labels;
        if (data.datasets) {
            data.datasets.forEach((dataset, index) => {
                if (chart.data.datasets[index]) {
                    chart.data.datasets[index].data = dataset.data;
                    if (dataset.label) chart.data.datasets[index].label = dataset.label;
                }
            });
        }

        chart.update();
    }

    destroyChart(elementId) {
        if (this.charts[elementId]) {
            this.charts[elementId].destroy();
            delete this.charts[elementId];
        }
    }

    destroyAllCharts() {
        Object.keys(this.charts).forEach(key => {
            this.charts[key].destroy();
        });
        this.charts = {};
    }

    // Generate gradient for charts
    createGradient(ctx, color1, color2) {
        const gradient = ctx.createLinearGradient(0, 0, 0, 400);
        gradient.addColorStop(0, color1);
        gradient.addColorStop(1, color2);
        return gradient;
    }
}

window.ChartManager = ChartManager;