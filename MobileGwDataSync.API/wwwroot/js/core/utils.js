// utils.js - Utility functions
class Utils {
    // Format numbers with thousands separator
    static formatNumber(num) {
        if (num === null || num === undefined) return '0';
        return new Intl.NumberFormat('en-US').format(num);
    }

    // Format bytes to human readable
    static formatBytes(bytes, decimals = 2) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const dm = decimals < 0 ? 0 : decimals;
        const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
    }

    // Format duration
    static formatDuration(seconds) {
        if (!seconds || seconds < 0) return '-';

        const hours = Math.floor(seconds / 3600);
        const minutes = Math.floor((seconds % 3600) / 60);
        const secs = Math.floor(seconds % 60);

        if (hours > 0) {
            return `${hours}h ${minutes}m ${secs}s`;
        } else if (minutes > 0) {
            return `${minutes}m ${secs}s`;
        } else {
            return `${secs}s`;
        }
    }

    // Format date time
    static formatDateTime(date, format = 'full') {
        if (!date) return '-';

        const d = new Date(date);

        switch (format) {
            case 'date':
                return d.toLocaleDateString();
            case 'time':
                return d.toLocaleTimeString();
            case 'relative':
                return this.getRelativeTime(d);
            case 'full':
            default:
                return d.toLocaleString();
        }
    }

    // Get relative time (e.g., "2 hours ago")
    static getRelativeTime(date) {
        const now = new Date();
        const diff = now - new Date(date);
        const seconds = Math.floor(diff / 1000);
        const minutes = Math.floor(seconds / 60);
        const hours = Math.floor(minutes / 60);
        const days = Math.floor(hours / 24);

        if (days > 0) return `${days} day${days > 1 ? 's' : ''} ago`;
        if (hours > 0) return `${hours} hour${hours > 1 ? 's' : ''} ago`;
        if (minutes > 0) return `${minutes} minute${minutes > 1 ? 's' : ''} ago`;
        return `${seconds} second${seconds > 1 ? 's' : ''} ago`;
    }

    // Parse cron expression to human readable
    static parseCron(cron) {
        const parts = cron.split(' ');
        if (parts.length !== 6) return cron;

        const [second, minute, hour, dayOfMonth, month, dayOfWeek] = parts;

        // Simple cases
        if (minute === '0' && hour === '*') return 'Every hour';
        if (minute === '0' && hour === '0') return 'Daily at midnight';
        if (minute === '30' && hour === '2') return 'Daily at 2:30 AM';
        if (minute === '*/5') return 'Every 5 minutes';
        if (minute === '*/10') return 'Every 10 minutes';
        if (minute === '*/30') return 'Every 30 minutes';

        return cron;
    }

    // Debounce function
    static debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    // Throttle function
    static throttle(func, limit) {
        let inThrottle;
        return function (...args) {
            if (!inThrottle) {
                func.apply(this, args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        };
    }

    // Copy to clipboard
    static async copyToClipboard(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (err) {
            console.error('Failed to copy:', err);
            return false;
        }
    }

    // Generate UUID
    static generateUUID() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    // Download data as file
    static downloadFile(data, filename, type = 'application/json') {
        const blob = new Blob([data], { type });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
    }

    // Get status color class
    static getStatusClass(status) {
        const statusMap = {
            'completed': 'success',
            'success': 'success',
            'failed': 'danger',
            'error': 'danger',
            'inprogress': 'warning',
            'running': 'warning',
            'pending': 'secondary',
            'cancelled': 'secondary'
        };
        return statusMap[status.toLowerCase()] || 'secondary';
    }

    // Validate email
    static isValidEmail(email) {
        const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return re.test(email);
    }

    // Local storage wrapper with expiry
    static storage = {
        set(key, value, ttl = null) {
            const item = {
                value: value,
                timestamp: Date.now(),
                ttl: ttl
            };
            localStorage.setItem(key, JSON.stringify(item));
        },

        get(key) {
            const itemStr = localStorage.getItem(key);
            if (!itemStr) return null;

            try {
                const item = JSON.parse(itemStr);
                if (item.ttl && Date.now() - item.timestamp > item.ttl) {
                    localStorage.removeItem(key);
                    return null;
                }
                return item.value;
            } catch (e) {
                return null;
            }
        },

        remove(key) {
            localStorage.removeItem(key);
        }
    };
}

// Export for use
window.Utils = Utils;