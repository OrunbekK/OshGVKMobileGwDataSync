// tables.js - Table components
class TableManager {
    constructor() {
        this.tables = {};
        this.sortState = {};
    }

    createDataTable(elementId, config) {
        const container = document.getElementById(elementId);
        if (!container) return;

        const tableId = `table-${elementId}`;
        const table = {
            id: tableId,
            config: config,
            currentPage: 1,
            pageSize: config.pageSize || 10,
            searchTerm: ''
        };

        this.tables[elementId] = table;
        this.render(elementId);
        return table;
    }

    render(elementId) {
        const table = this.tables[elementId];
        const container = document.getElementById(elementId);

        let html = `
            <div class="table-wrapper">
                ${table.config.searchable ? this.renderSearch(elementId) : ''}
                ${table.config.actions ? this.renderActions(elementId) : ''}
                <div class="table-responsive">
                    <table class="table table-hover ${table.config.className || ''}">
                        ${this.renderHeader(elementId)}
                        ${this.renderBody(elementId)}
                    </table>
                </div>
                ${table.config.pagination ? this.renderPagination(elementId) : ''}
            </div>
        `;

        container.innerHTML = html;
        this.attachEventListeners(elementId);
    }

    renderSearch(elementId) {
        return `
            <div class="mb-3">
                <div class="input-group">
                    <span class="input-group-text"><i class="bi bi-search"></i></span>
                    <input type="text" class="form-control" placeholder="Search..." 
                           id="${elementId}-search" value="${this.tables[elementId].searchTerm}">
                </div>
            </div>
        `;
    }

    renderActions(elementId) {
        const table = this.tables[elementId];
        return `
            <div class="d-flex justify-content-between mb-3">
                <div>
                    ${table.config.title ? `<h5>${table.config.title}</h5>` : ''}
                </div>
                <div>
                    ${table.config.actions.map(action => `
                        <button class="btn btn-${action.type || 'primary'} btn-sm" 
                                onclick="${action.handler}">
                            ${action.icon ? `<i class="${action.icon} me-2"></i>` : ''}
                            ${action.label}
                        </button>
                    `).join(' ')}
                </div>
            </div>
        `;
    }

    renderHeader(elementId) {
        const table = this.tables[elementId];
        return `
            <thead>
                <tr>
                    ${table.config.columns.map((col, index) => `
                        <th ${col.sortable ? `class="sortable" data-column="${index}"` : ''}>
                            ${col.label}
                            ${col.sortable ? '<i class="bi bi-chevron-expand ms-1"></i>' : ''}
                        </th>
                    `).join('')}
                    ${table.config.rowActions ? '<th>Actions</th>' : ''}
                </tr>
            </thead>
        `;
    }

    renderBody(elementId) {
        const table = this.tables[elementId];
        const data = this.getFilteredData(elementId);

        if (!data || data.length === 0) {
            return `
                <tbody>
                    <tr>
                        <td colspan="${table.config.columns.length + (table.config.rowActions ? 1 : 0)}" 
                            class="text-center text-muted py-4">
                            No data available
                        </td>
                    </tr>
                </tbody>
            `;
        }

        const start = (table.currentPage - 1) * table.pageSize;
        const end = start + table.pageSize;
        const pageData = data.slice(start, end);

        return `
            <tbody>
                ${pageData.map(row => `
                    <tr>
                        ${table.config.columns.map(col => `
                            <td>${this.renderCell(row, col)}</td>
                        `).join('')}
                        ${table.config.rowActions ? `
                            <td>
                                ${table.config.rowActions.map(action => `
                                    <button class="btn btn-${action.type || 'primary'} btn-sm" 
                                            onclick="${action.handler}('${row.id || ''}')">
                                        ${action.icon ? `<i class="${action.icon}"></i>` : action.label}
                                    </button>
                                `).join(' ')}
                            </td>
                        ` : ''}
                    </tr>
                `).join('')}
            </tbody>
        `;
    }

    renderCell(row, column) {
        const value = row[column.field];

        if (column.formatter) {
            return column.formatter(value, row);
        }

        switch (column.type) {
            case 'date':
                return Utils.formatDateTime(value, 'date');
            case 'datetime':
                return Utils.formatDateTime(value);
            case 'number':
                return Utils.formatNumber(value);
            case 'badge':
                return `<span class="badge bg-${Utils.getStatusClass(value)}">${value}</span>`;
            case 'boolean':
                return value ? '<i class="bi bi-check-circle text-success"></i>' :
                    '<i class="bi bi-x-circle text-danger"></i>';
            default:
                return value || '-';
        }
    }

    renderPagination(elementId) {
        const table = this.tables[elementId];
        const data = this.getFilteredData(elementId);
        const totalPages = Math.ceil(data.length / table.pageSize);

        if (totalPages <= 1) return '';

        const pages = this.generatePageNumbers(table.currentPage, totalPages);

        return `
            <nav class="mt-3">
                <ul class="pagination justify-content-center">
                    <li class="page-item ${table.currentPage === 1 ? 'disabled' : ''}">
                        <a class="page-link" href="#" data-page="prev">Previous</a>
                    </li>
                    ${pages.map(page => `
                        <li class="page-item ${page === table.currentPage ? 'active' : ''} ${page === '...' ? 'disabled' : ''}">
                            <a class="page-link" href="#" data-page="${page}">${page}</a>
                        </li>
                    `).join('')}
                    <li class="page-item ${table.currentPage === totalPages ? 'disabled' : ''}">
                        <a class="page-link" href="#" data-page="next">Next</a>
                    </li>
                </ul>
            </nav>
        `;
    }

    generatePageNumbers(current, total) {
        const pages = [];
        const maxVisible = 5;

        if (total <= maxVisible) {
            for (let i = 1; i <= total; i++) {
                pages.push(i);
            }
        } else {
            if (current <= 3) {
                for (let i = 1; i <= 4; i++) pages.push(i);
                pages.push('...');
                pages.push(total);
            } else if (current >= total - 2) {
                pages.push(1);
                pages.push('...');
                for (let i = total - 3; i <= total; i++) pages.push(i);
            } else {
                pages.push(1);
                pages.push('...');
                pages.push(current - 1);
                pages.push(current);
                pages.push(current + 1);
                pages.push('...');
                pages.push(total);
            }
        }

        return pages;
    }

    attachEventListeners(elementId) {
        const table = this.tables[elementId];
        const container = document.getElementById(elementId);

        // Search
        const searchInput = container.querySelector(`#${elementId}-search`);
        if (searchInput) {
            searchInput.addEventListener('input', Utils.debounce((e) => {
                table.searchTerm = e.target.value;
                table.currentPage = 1;
                this.render(elementId);
            }, 300));
        }

        // Sort
        container.querySelectorAll('.sortable').forEach(th => {
            th.addEventListener('click', () => {
                const columnIndex = parseInt(th.dataset.column);
                this.sort(elementId, columnIndex);
            });
        });

        // Pagination
        container.querySelectorAll('.page-link').forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                const page = e.target.dataset.page;
                this.changePage(elementId, page);
            });
        });
    }

    getFilteredData(elementId) {
        const table = this.tables[elementId];
        let data = [...(table.config.data || [])];

        // Search filter
        if (table.searchTerm) {
            const searchLower = table.searchTerm.toLowerCase();
            data = data.filter(row => {
                return table.config.columns.some(col => {
                    const value = row[col.field];
                    return value && value.toString().toLowerCase().includes(searchLower);
                });
            });
        }

        // Sort
        const sortState = this.sortState[elementId];
        if (sortState) {
            const column = table.config.columns[sortState.column];
            data.sort((a, b) => {
                const aVal = a[column.field];
                const bVal = b[column.field];
                const result = aVal > bVal ? 1 : aVal < bVal ? -1 : 0;
                return sortState.direction === 'asc' ? result : -result;
            });
        }

        return data;
    }

    sort(elementId, columnIndex) {
        const currentSort = this.sortState[elementId];

        if (currentSort && currentSort.column === columnIndex) {
            if (currentSort.direction === 'asc') {
                this.sortState[elementId].direction = 'desc';
            } else {
                delete this.sortState[elementId];
            }
        } else {
            this.sortState[elementId] = {
                column: columnIndex,
                direction: 'asc'
            };
        }

        this.render(elementId);
    }

    changePage(elementId, page) {
        const table = this.tables[elementId];
        const data = this.getFilteredData(elementId);
        const totalPages = Math.ceil(data.length / table.pageSize);

        if (page === 'prev' && table.currentPage > 1) {
            table.currentPage--;
        } else if (page === 'next' && table.currentPage < totalPages) {
            table.currentPage++;
        } else if (page !== '...' && !isNaN(page)) {
            table.currentPage = parseInt(page);
        }

        this.render(elementId);
    }

    updateData(elementId, data) {
        const table = this.tables[elementId];
        if (table) {
            table.config.data = data;
            this.render(elementId);
        }
    }

    exportToCSV(elementId) {
        const table = this.tables[elementId];
        const data = this.getFilteredData(elementId);

        const headers = table.config.columns.map(col => col.label).join(',');
        const rows = data.map(row =>
            table.config.columns.map(col => {
                const value = row[col.field];
                return typeof value === 'string' && value.includes(',') ?
                    `"${value}"` : value;
            }).join(',')
        );

        const csv = [headers, ...rows].join('\n');
        Utils.downloadFile(csv, `export-${Date.now()}.csv`, 'text/csv');
    }
}

window.TableManager = TableManager;