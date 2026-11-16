(() => {
    "use strict";

    const DATA = JSON.parse(document.getElementById("report-data").textContent);
    const DEVICES = DATA.devices || [];
    const SESSIONS = (DATA.sessions || []).slice().sort((a, b) => new Date(a.startTime) - new Date(b.startTime));
    const TASK_CATALOG = DATA.taskCatalog || [];
    const DEVICE_MAP = new Map(DEVICES.map(device => [device.deviceKey, device]));
    const SESSION_MAP = new Map(SESSIONS.map(session => [session.sessionKey, session]));
    const SESSIONS_BY_DEVICE = new Map();

    DEVICES.forEach(device => {
        SESSIONS_BY_DEVICE.set(
            device.deviceKey,
            SESSIONS.filter(session => session.deviceKey === device.deviceKey)
        );
    });

    const state = {
        view: "overview",
        deviceKey: "all",
        sessionKey: null,
        compareMode: "same",
        compareDeviceKey: null,
        sameSelected: new Set(),
        sameBaseline: null,
        sameSelectionInitialized: false,
        crossSelection: {},
        crossBaseline: null,
        overlayMetric: "cpuTotal",
        theme: "light",
        charts: []
    };

    const $ = (selector, root = document) => root.querySelector(selector);
    const $$ = (selector, root = document) => Array.from(root.querySelectorAll(selector));

    // ---------------------------------------------------------------- helpers

    const pad = value => String(value).padStart(2, "0");

    const esc = value => String(value ?? "").replace(/[&<>"']/g, char => ({
        "&": "&amp;",
        "<": "&lt;",
        ">": "&gt;",
        "\"": "&quot;",
        "'": "&#39;"
    }[char]));

    const num = (value, digits = 1) => {
        if (value === null || value === undefined || !Number.isFinite(Number(value))) return "—";
        return Number(value).toFixed(digits);
    };

    const pct = (value, digits = 1) => value === null || value === undefined ? "—" : `${num(value, digits)}%`;

    const gb = (value, digits = 2) => value === null || value === undefined ? "—" : `${num(value, digits)} GB`;

    const ms = value => !value || Number(value) <= 0 ? "—" : `${num(value, 0)} ms`;

    const rate = (value, unit) => {
        if (!value || Number(value) <= 0) return "—";
        if (unit === "KB/s" && value >= 1024) return `${num(value / 1024, 2)} MB/s`;
        if (unit === "Kbps" && value >= 1000) return `${num(value / 1000, 2)} Mbps`;
        return `${num(value, 1)} ${unit}`;
    };

    const duration = seconds => {
        const total = Math.max(0, Math.round(Number(seconds) || 0));
        const hours = Math.floor(total / 3600);
        const minutes = Math.floor((total % 3600) / 60);
        const secs = total % 60;
        return hours > 0 ? `${pad(hours)}:${pad(minutes)}:${pad(secs)}` : `${pad(minutes)}:${pad(secs)}`;
    };

    const axisTime = seconds => {
        const total = Math.max(0, Number(seconds) || 0);
        if (total >= 3600) {
            return `${Math.floor(total / 3600)}:${pad(Math.floor((total % 3600) / 60))}:${pad(Math.floor(total % 60))}`;
        }
        return `${pad(Math.floor(total / 60))}:${pad(Math.floor(total % 60))}`;
    };

    const dateTime = iso => {
        const date = new Date(iso);
        if (Number.isNaN(date.getTime())) return "—";
        return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
    };

    const shortDate = iso => {
        const date = new Date(iso);
        if (Number.isNaN(date.getTime())) return "—";
        return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
    };

    const signed = (value, digits, unit) => {
        if (value === null || value === undefined || !Number.isFinite(Number(value))) return "—";
        const number = Number(value);
        if (Math.abs(number) < 1e-9) return `0${unit}`;
        return `${number > 0 ? "+" : ""}${number.toFixed(digits)}${unit}`;
    };

    const deltaClass = (value, lowerIsBetter = true) => {
        if (value === null || value === undefined || !Number.isFinite(Number(value)) || Math.abs(Number(value)) < 1e-9) {
            return "delta-neutral";
        }
        const improved = lowerIsBetter ? Number(value) < 0 : Number(value) > 0;
        return improved ? "delta-good" : "delta-bad";
    };

    const baselineKeyFor = deviceKey => DEVICE_MAP.get(deviceKey)?.baselineSessionKey || null;

    const baselineFor = session => {
        const key = baselineKeyFor(session.deviceKey);
        return key ? SESSION_MAP.get(key) || null : null;
    };

    const deltaAgainst = (session, baseline, selector) => {
        if (!baseline || baseline.sessionKey === session.sessionKey) return null;
        return Number(selector(session)) - Number(selector(baseline));
    };

    const filteredSessions = () => state.deviceKey === "all"
        ? SESSIONS
        : SESSIONS.filter(session => session.deviceKey === state.deviceKey);

    const latestSession = sessions => sessions.length ? sessions[sessions.length - 1] : null;

    const taskOf = (session, name) => (session.tasks || []).find(task => task.name === name) || null;

    const themeColors = () => {
        const dark = state.theme === "dark";
        return {
            dark,
            text: dark ? "#f0f4f8" : "#14213d",
            muted: dark ? "#9fb0c6" : "#64748b",
            grid: dark ? "#1f3555" : "#e8eef7",
            tooltipBg: dark ? "#14243d" : "#ffffff",
            border: dark ? "#2a405e" : "#dbe3ef",
            panel: dark ? "#14243d" : "#ffffff"
        };
    };

    // ---------------------------------------------------------------- charts

    const disposeCharts = () => {
        state.charts.forEach(chart => {
            try { chart.dispose(); } catch { /* ignore */ }
        });
        state.charts = [];
    };

    const initChart = (id, option) => {
        if (typeof echarts === "undefined") return;
        const element = document.getElementById(id);
        if (!element) return;
        const chart = echarts.init(element, null, { renderer: "canvas" });
        chart.setOption(option);
        state.charts.push(chart);
    };

    const lineOption = (series, options = {}) => {
        const colors = themeColors();
        const unit = options.unit || "";
        return {
            animation: false,
            backgroundColor: "transparent",
            color: series.map(item => item.color).filter(Boolean),
            grid: { left: 58, right: 22, top: 46, bottom: 62 },
            tooltip: {
                trigger: "axis",
                backgroundColor: colors.tooltipBg,
                borderColor: colors.border,
                textStyle: { color: colors.text, fontSize: 12 },
                valueFormatter: value => `${num(value, 2)}${unit}`
            },
            legend: {
                top: 8,
                type: "scroll",
                textStyle: { color: colors.muted, fontSize: 11 },
                pageTextStyle: { color: colors.muted }
            },
            xAxis: {
                type: "value",
                name: options.xName || "經過時間",
                nameTextStyle: { color: colors.muted, fontSize: 11 },
                axisLabel: { color: colors.muted, fontSize: 10, formatter: axisTime },
                axisLine: { lineStyle: { color: colors.border } },
                splitLine: { lineStyle: { color: colors.grid, type: "dashed" } }
            },
            yAxis: {
                type: "value",
                name: options.yName || "",
                max: options.yMax,
                nameTextStyle: { color: colors.muted, fontSize: 11 },
                axisLabel: { color: colors.muted, fontSize: 10 },
                axisLine: { lineStyle: { color: colors.border } },
                splitLine: { lineStyle: { color: colors.grid, type: "dashed" } }
            },
            dataZoom: [
                { type: "inside" },
                {
                    type: "slider",
                    height: 14,
                    bottom: 8,
                    borderColor: colors.border,
                    textStyle: { color: colors.muted, fontSize: 10 },
                    fillerColor: colors.dark ? "rgba(66,165,245,.14)" : "rgba(21,101,192,.08)"
                }
            ],
            series: series.map(item => ({
                name: item.name,
                type: "line",
                showSymbol: false,
                symbol: "none",
                lineStyle: { width: item.width || 1.6 },
                areaStyle: item.area ? { opacity: 0.06 } : undefined,
                emphasis: { focus: "series" },
                data: item.points || []
            }))
        };
    };

    const trendOption = (labels, series, options = {}) => {
        const colors = themeColors();
        return {
            animation: false,
            backgroundColor: "transparent",
            color: series.map(item => item.color),
            grid: { left: 56, right: 22, top: 44, bottom: 62 },
            tooltip: {
                trigger: "axis",
                backgroundColor: colors.tooltipBg,
                borderColor: colors.border,
                textStyle: { color: colors.text, fontSize: 12 },
                valueFormatter: value => `${num(value, 1)}${options.unit || ""}`
            },
            legend: { top: 8, textStyle: { color: colors.muted, fontSize: 11 } },
            xAxis: {
                type: "category",
                data: labels,
                axisLabel: { color: colors.muted, fontSize: 10, rotate: 28 },
                axisLine: { lineStyle: { color: colors.border } }
            },
            yAxis: {
                type: "value",
                max: options.yMax,
                axisLabel: { color: colors.muted, fontSize: 10 },
                splitLine: { lineStyle: { color: colors.grid, type: "dashed" } }
            },
            series: series.map(item => ({
                name: item.name,
                type: "line",
                showSymbol: true,
                symbolSize: 5,
                lineStyle: { width: 1.6 },
                data: item.values
            }))
        };
    };

    const renderOverviewCharts = sessions => {
        const ordered = sessions.slice().sort((a, b) => new Date(a.startTime) - new Date(b.startTime));
        if (!ordered.length) return;
        const labels = ordered.map(session => shortDate(session.startTime));

        initChart("chart-cpu-trend", trendOption(labels, [
            { name: "CPU 平均", color: "#1565c0", values: ordered.map(session => session.metrics.cpuAvg) },
            { name: "CPU 最高", color: "#c62828", values: ordered.map(session => session.metrics.cpuMax) }
        ], { yMax: 100, unit: "%" }));

        initChart("chart-ram-trend", trendOption(labels, [
            { name: "RAM 平均", color: "#6a1b9a", values: ordered.map(session => session.metrics.ramAvg) },
            { name: "RAM 最高", color: "#ef6c00", values: ordered.map(session => session.metrics.ramMax) }
        ], { unit: " GB" }));
    };

    const seriesOf = (session, key) => (session.series || []).find(series => series.key === key);

    const systemChart = (id, session, keys) => {
        const items = keys
            .map(key => seriesOf(session, key))
            .filter(Boolean)
            .map(series => ({
                name: series.name,
                color: series.color,
                unit: series.unit,
                points: series.points
            }));
        initChart(id, lineOption(items, { unit: items[0]?.unit || "", yMax: keys.includes("cpuTotal") ? 100 : undefined }));
    };

    const taskChart = (id, session, valueKey, unit) => {
        const tasks = (session.tasks || []).filter(task => (task[valueKey] || []).length > 0);
        initChart(id, lineOption(tasks.map(task => ({
            name: task.name,
            color: task.color,
            points: task[valueKey]
        })), { unit }));
    };

    const renderSessionCharts = session => {
        systemChart("chart-session-cpu", session, ["cpuTotal", "cpuSimulator", "cpuFree"]);
        systemChart("chart-session-ram", session, ["memoryUsed", "memoryAvailable"]);
        taskChart("chart-task-cpu", session, "cpuPoints", "%");
        taskChart("chart-task-memory", session, "memoryPoints", " MB");
    };

    const COMPARE_COLORS = ["#1565c0", "#00897b", "#2e7d32", "#ef6c00", "#c62828", "#6a1b9a", "#5d4037", "#0277bd"];

    const renderCompareChart = (containerId, sessions, metricKey, unit) => {
        const items = sessions
            .map((session, index) => {
                const series = seriesOf(session, metricKey);
                return series ? {
                    name: `${session.folderName} ・ ${session.deviceDisplay}`,
                    color: COMPARE_COLORS[index % COMPARE_COLORS.length],
                    points: series.points
                } : null;
            })
            .filter(Boolean);
        initChart(containerId, lineOption(items, { unit }));
    };

    // ---------------------------------------------------------------- views

    const render = () => {
        disposeCharts();
        document.body.classList.toggle("dark", state.theme === "dark");
        $("#theme-btn").textContent = state.theme === "dark" ? "淺色" : "深色";
        renderDeviceSelect();
        $$("#tabs button").forEach(button => {
            button.classList.toggle("active", button.dataset.view === state.view);
        });

        const main = $("#main");
        if (state.view === "session") {
            main.innerHTML = renderSessionView();
        } else if (state.view === "compare") {
            main.innerHTML = renderCompareView();
        } else {
            main.innerHTML = renderOverviewView();
        }

        if (state.view === "overview") {
            renderOverviewCharts(filteredSessions());
        } else if (state.view === "session") {
            const session = currentSession();
            if (session) renderSessionCharts(session);
        } else if (state.view === "compare") {
            if (state.compareMode === "same") {
                const sessions = sameCompareSessions();
                renderCompareChart("chart-compare-overlay", sessions, state.overlayMetric, state.overlayMetric === "memoryUsed" ? " GB" : "%");
            } else {
                renderCompareChart("chart-cross-overlay", crossCompareSessions(), state.overlayMetric, state.overlayMetric === "memoryUsed" ? " GB" : "%");
            }
        }

        updateHash();
    };

    const deviceCard = device => {
        const hardware = [
            device.cpuModel,
            device.cores ? `${device.cores}C` : null,
            device.ramGb ? `${num(device.ramGb, 1)} GB` : null,
            device.osName,
            device.osBuild ? `Build ${device.osBuild}` : null,
            device.gpu
        ].filter(Boolean).join(" ・ ");

        const baseline = device.baselineSessionKey ? SESSION_MAP.get(device.baselineSessionKey) : null;

        return `
            <div class="card">
                <div class="detail-head">
                    <div>
                        <h2>${esc(device.displayName)}</h2>
                        <div class="hardware">${esc(hardware || "硬體資訊不完整")}</div>
                        <div class="meta">
                            <span>${device.sessionCount} 場</span>
                            ${baseline ? `<span>基準：${esc(baseline.folderName)}</span>` : "<span>尚未設定基準</span>"}
                            <span class="badge ${device.isIdentified ? "teal" : "amber"}">${device.isIdentified ? "已識別" : "未識別"}</span>
                        </div>
                    </div>
                    <button class="btn ghost no-print" data-action="all-devices" type="button">查看全部裝置</button>
                </div>
            </div>`;
    };

    const kpi = (label, value, detail, tone = "") => `
        <div class="kpi ${tone}">
            <div class="label">${esc(label)}</div>
            <div class="value">${esc(value)}</div>
            <div class="detail">${esc(detail)}</div>
        </div>`;

    const renderOverviewView = () => {
        const sessions = filteredSessions();
        const devices = state.deviceKey === "all"
            ? DEVICES
            : DEVICES.filter(device => device.deviceKey === state.deviceKey);
        const taskCount = new Set(sessions.flatMap(session => (session.tasks || []).map(task => task.name))).size;
        const warnings = sessions.reduce((total, session) => total + session.warningCount, 0);
        const errors = sessions.reduce((total, session) => total + session.errorCount, 0);
        const device = state.deviceKey === "all" ? null : DEVICE_MAP.get(state.deviceKey);

        const content = `
            ${device ? deviceCard(device) : ""}
            <div class="grid kpi ${device ? "section" : ""}">
                ${kpi("檢測場次", sessions.length, device ? "目前裝置的場次" : "已載入的監測場次", "")}
                ${kpi("裝置數量", devices.length, "依裝置指紋分組", "teal")}
                ${kpi("任務種類", taskCount, "跨場次任務種類", "green")}
                ${kpi("需要注意", warnings + errors, `${warnings} 警告 ・ ${errors} 錯誤`, "amber")}
            </div>

            <div class="grid two section">
                <div class="card">
                    <div class="card-title"><h2>歷次 CPU 趨勢</h2><span class="sub">平均 / 最高</span></div>
                    <div id="chart-cpu-trend" class="chart"></div>
                </div>
                <div class="card">
                    <div class="card-title"><h2>歷次 RAM 趨勢</h2><span class="sub">平均 / 最高</span></div>
                    <div id="chart-ram-trend" class="chart"></div>
                </div>
            </div>

            <div class="card section">
                <div class="card-title"><h2>檢測紀錄</h2><span class="sub">點擊列可進入單場次</span></div>
                <div class="table-wrap tall">${sessionTable(sessions)}</div>
            </div>

            <div class="card section">
                <div class="card-title"><h2>任務覆蓋矩陣</h2><span class="sub">CPU 平均 / 未執行</span></div>
                ${coverageTable(sessions)}
            </div>

            <div class="card section">
                <div class="card-title"><h2>資料品質</h2><span class="sub">缺少檔案、跳秒、任務未執行</span></div>
                ${qualityTable(sessions)}
            </div>`;

        return content;
    };

    const sessionTable = sessions => {
        if (!sessions.length) return `<div class="empty">沒有場次資料</div>`;
        const ordered = sessions.slice().sort((a, b) => new Date(b.startTime) - new Date(a.startTime));

        const rows = ordered.map(session => {
            const baseline = baselineFor(session);
            const cpuDelta = deltaAgainst(session, baseline, s => s.metrics.cpuAvg);
            const ramDelta = deltaAgainst(session, baseline, s => s.metrics.ramAvg);
            const isBaseline = baseline && baseline.sessionKey === session.sessionKey;

            return `
                <tr class="clickable" data-action="open-session" data-key="${esc(session.sessionKey)}">
                    <td>${esc(session.folderName)}</td>
                    <td class="nowrap">${esc(session.deviceDisplay)}</td>
                    <td class="nowrap">${dateTime(session.startTime)}</td>
                    <td class="num">${duration(session.durationSeconds)}</td>
                    <td class="num">${num(session.metrics.cpuAvg)}</td>
                    <td class="num">${num(session.metrics.cpuMax)}</td>
                    <td class="num ${isBaseline ? "delta-neutral" : deltaClass(cpuDelta)}">${isBaseline ? "基準" : signed(cpuDelta, 1, "%")}</td>
                    <td class="num">${num(session.metrics.ramAvg, 2)}</td>
                    <td class="num">${num(session.metrics.ramMax, 2)}</td>
                    <td class="num ${isBaseline ? "delta-neutral" : deltaClass(ramDelta)}">${isBaseline ? "基準" : signed(ramDelta, 2, " GB")}</td>
                    <td class="num">${(session.tasks || []).length}</td>
                    <td class="num">${session.warningCount + session.errorCount}</td>
                    <td class="center">${session.isBaseline ? "★" : ""}</td>
                </tr>`;
        }).join("");

        return `
            <table>
                <thead><tr>
                    <th>場次</th><th>裝置</th><th>開始</th><th class="num">時長</th>
                    <th class="num">CPU 平均</th><th class="num">CPU 最高</th><th class="num">ΔCPU</th>
                    <th class="num">RAM 平均</th><th class="num">RAM 最高</th><th class="num">ΔRAM</th>
                    <th class="num">任務</th><th class="num">警告</th><th class="center">基準</th>
                </tr></thead>
                <tbody>${rows}</tbody>
            </table>`;
    };

    const coverageTable = sessions => {
        if (!sessions.length || !TASK_CATALOG.length) return `<div class="empty">沒有任務資料</div>`;
        const ordered = sessions.slice().sort((a, b) => new Date(b.startTime) - new Date(a.startTime));

        const header = TASK_CATALOG.map(name => `<th class="center">${esc(name)}</th>`).join("");
        const rows = ordered.map(session => {
            const cells = TASK_CATALOG.map(name => {
                const task = taskOf(session, name);
                if (!task) return `<td class="cell none"><span class="inner">—</span></td>`;
                if (!task.runCount) return `<td class="cell stop"><span class="inner">未執行</span></td>`;
                return `<td class="cell run" title="執行 ${duration(task.activeSeconds)}，CPU 平均 ${num(task.cpuAvg)}%，最高 ${num(task.cpuMax)}%">
                    <span class="inner">${num(task.cpuAvg)}%</span>
                </td>`;
            }).join("");

            return `<tr>
                <td><div>${esc(session.folderName)}</div><div class="muted" style="font-size:11px">${esc(session.deviceDisplay)}</div></td>
                ${cells}
            </tr>`;
        }).join("");

        return `<div class="coverage-wrap">
            <table class="coverage">
                <thead><tr><th>場次 / 裝置</th>${header}</tr></thead>
                <tbody>${rows}</tbody>
            </table>
        </div>`;
    };

    const qualityTable = sessions => {
        const issues = sessions.filter(session => session.warningCount + session.errorCount > 0 || !session.hasRefTxt || !session.hasSystemLog);
        if (!issues.length) return `<div class="empty">沒有警告或錯誤</div>`;

        const rows = issues.map(session => {
            const messages = (session.diagnostics || [])
                .filter(diagnostic => diagnostic.severity !== "Info")
                .slice(0, 3)
                .map(diagnostic => diagnostic.message)
                .join("；");
            const flags = [
                session.hasRefTxt ? null : "缺 ref.txt",
                session.hasSystemLog ? null : "缺系統 CSV"
            ].filter(Boolean).join(" ・ ");

            return `<tr>
                <td>${esc(session.folderName)}</td>
                <td class="nowrap">${esc(session.deviceDisplay)}</td>
                <td class="num">${session.warningCount}</td>
                <td class="num">${session.errorCount}</td>
                <td>${esc(flags || messages || "—")}</td>
            </tr>`;
        }).join("");

        return `<div class="table-wrap">
            <table>
                <thead><tr><th>場次</th><th>裝置</th><th class="num">警告</th><th class="num">錯誤</th><th>摘要</th></tr></thead>
                <tbody>${rows}</tbody>
            </table>
        </div>`;
    };

    // ---------------------------------------------------------------- session

    const currentSession = () => {
        const sessions = filteredSessions();
        if (sessions.some(session => session.sessionKey === state.sessionKey)) {
            return SESSION_MAP.get(state.sessionKey);
        }
        return latestSession(sessions);
    };

    const renderSessionView = () => {
        const sessions = filteredSessions();
        const session = currentSession();
        if (!session) return `<div class="empty">沒有場次資料</div>`;

        state.sessionKey = session.sessionKey;
        const device = DEVICE_MAP.get(session.deviceKey);
        const baseline = baselineFor(session);
        const isBaseline = baseline && baseline.sessionKey === session.sessionKey;
        const options = sessions.map(item => `<option value="${esc(item.sessionKey)}" ${item.sessionKey === session.sessionKey ? "selected" : ""}>${esc(item.folderName)} ・ ${esc(item.deviceDisplay)}</option>`).join("");

        const hardware = [
            device?.cpuModel,
            device?.cores ? `${device.cores}C` : null,
            device?.ramGb ? `${num(device.ramGb, 1)} GB` : null,
            device?.osName,
            device?.osBuild ? `Build ${device.osBuild}` : null
        ].filter(Boolean).join(" ・ ");

        const baselineText = !baseline
            ? "尚未設定基準"
            : isBaseline
                ? "此場次為基準"
                : `vs 基準 ${esc(baseline.folderName)}：CPU ${signed(deltaAgainst(session, baseline, s => s.metrics.cpuAvg), 1, "%")} ・ RAM ${signed(deltaAgainst(session, baseline, s => s.metrics.ramAvg), 2, " GB")}`;

        const labels = (session.labels || []).map(label => `<span class="badge blue">${esc(label)}</span>`).join(" ");

        return `
            <div class="card">
                <div class="detail-head">
                    <div>
                        <h2>${esc(session.folderName)}</h2>
                        <div class="badges">
                            <span class="badge teal">${esc(session.deviceDisplay)}</span>
                            ${isBaseline ? `<span class="badge amber">★ 基準場次</span>` : ""}
                            ${session.warningCount + session.errorCount > 0 ? `<span class="badge amber">${session.warningCount} 警告 ・ ${session.errorCount} 錯誤</span>` : ""}
                            ${labels}
                        </div>
                        <div class="hardware">${esc(hardware || "硬體資訊不完整")}</div>
                        <div class="meta">
                            <span>${dateTime(session.startTime)} → ${dateTime(session.endTime).slice(6)}</span>
                            <span>${duration(session.durationSeconds)}</span>
                            <span>樣本 ${session.sampleCount}</span>
                            <span>${esc(session.mode || "—")}</span>
                        </div>
                        <div class="meta">${baselineText}</div>
                    </div>
                    <div class="toolbar no-print" style="margin:0">
                        <label>場次 <select data-role="session-select">${options}</select></label>
                        <button class="btn ghost" data-action="view" data-view="overview" type="button">回總覽</button>
                    </div>
                </div>
            </div>

            <div class="grid kpi section">
                ${kpi("CPU 平均", pct(session.metrics.cpuAvg), `最高 ${pct(session.metrics.cpuMax)} ・ p95 ${pct(session.metrics.cpuP95)}`, "")}
                ${kpi("RAM 平均", gb(session.metrics.ramAvg), `最高 ${gb(session.metrics.ramMax)} ・ 使用率 ${pct(session.metrics.ramPercentMax, 0)}`, "purple")}
                ${kpi("磁碟峰值", rate(session.metrics.diskPeak, "KB/s"), "任務最高", "teal")}
                ${kpi("網路峰值", rate(session.metrics.networkPeak, "Kbps"), "任務最高", "green")}
                ${kpi("辨識時長", ms(session.metrics.recognitionAvg), `最高 ${ms(session.metrics.recognitionMax)}`, "amber")}
                ${kpi("資料完整率", pct(session.metrics.completeness * 100, 0), `樣本 ${session.sampleCount} / 預期 ${session.expectedSampleCount}`, "")}
            </div>

            <div class="grid two section">
                <div class="card">
                    <div class="card-title"><h2>系統 CPU</h2><span class="sub">全系統 / 模擬器 / 閒置</span></div>
                    <div id="chart-session-cpu" class="chart"></div>
                </div>
                <div class="card">
                    <div class="card-title"><h2>系統記憶體</h2><span class="sub">已用 / 可用</span></div>
                    <div id="chart-session-ram" class="chart"></div>
                </div>
            </div>

            <div class="card section">
                <div class="card-title"><h2>任務執行時間軸</h2><span class="sub">空白＝未執行</span></div>
                ${gantt(session)}
            </div>

            <div class="grid two section">
                <div class="card">
                    <div class="card-title"><h2>任務 CPU</h2><span class="sub">只含執行中樣本</span></div>
                    <div id="chart-task-cpu" class="chart"></div>
                </div>
                <div class="card">
                    <div class="card-title"><h2>任務記憶體</h2><span class="sub">只含執行中樣本</span></div>
                    <div id="chart-task-memory" class="chart"></div>
                </div>
            </div>

            <div class="card section">
                <div class="card-title"><h2>任務統計</h2><span class="sub">共 ${(session.tasks || []).length} 個任務</span></div>
                ${taskTable(session)}
            </div>

            ${session.notes || (session.labels || []).length ? `
            <div class="card section">
                <div class="card-title"><h2>備註與標籤</h2></div>
                <div>${labels || '<span class="muted">沒有標籤</span>'}</div>
                ${session.notes ? `<p class="note" style="margin:8px 0 0; white-space:pre-wrap">${esc(session.notes)}</p>` : ""}
            </div>` : ""}

            <div class="card section">
                <div class="card-title"><h2>診斷與資料品質</h2><span class="sub">${session.warningCount} 警告 ・ ${session.errorCount} 錯誤</span></div>
                ${diagnostics(session)}
            </div>`;
    };

    const gantt = session => {
        const tasks = session.tasks || [];
        const total = Math.max(1, session.durationSeconds);
        if (!tasks.length) return `<div class="empty">沒有任務資料</div>`;

        const rows = tasks.map(task => {
            const bars = (task.runs || []).map(run => {
                const left = Math.max(0, Math.min(100, run.startSeconds / total * 100));
                const width = Math.max(0.4, Math.min(100 - left, run.durationSeconds / total * 100));
                return `<div class="gantt-bar" style="left:${left}%;width:${width}%;background:${task.color}"></div>`;
            }).join("");

            const label = task.runCount
                ? esc(task.name)
                : `<span class="muted">${esc(task.name)}（未執行）</span>`;

            return `<div class="gantt-row">
                <div class="gantt-label" title="${esc(task.name)}">${label}</div>
                <div class="gantt-track">${bars}</div>
            </div>`;
        }).join("");

        const axis = [0, 0.25, 0.5, 0.75, 1]
            .map(fraction => `<span>${axisTime(total * fraction)}</span>`)
            .join("");

        return `<div class="gantt">${rows}</div><div class="gantt-axis">${axis}</div>`;
    };

    const taskTable = session => {
        const tasks = session.tasks || [];
        if (!tasks.length) return `<div class="empty">沒有任務紀錄</div>`;

        const rows = tasks.map(task => {
            const hasRun = task.runCount > 0;
            return `<tr>
                <td><span class="badge" style="background:${task.color};color:#fff;border-color:${task.color}">${esc(task.name)}</span></td>
                <td>${hasRun ? "已執行" : "未執行"}</td>
                <td class="num">${duration(task.activeSeconds)}</td>
                <td class="num">${pct(task.activeRatio * 100, 0)}</td>
                <td class="num">${hasRun ? `${num(task.cpuAvg)} / ${num(task.cpuMax)}` : "—"}</td>
                <td class="num">${hasRun ? `${num(task.memoryAvg)} / ${num(task.memoryMax)}` : "—"}</td>
                <td class="num">${num(task.cpuSeconds)}</td>
                <td class="num">${rate(task.diskPeak, "KB/s")}</td>
                <td class="num">${rate(task.networkPeak, "Kbps")}</td>
                <td class="num">${task.runCount} / ${task.restartCount}</td>
            </tr>`;
        }).join("");

        return `<div class="table-wrap">
            <table>
                <thead><tr>
                    <th>任務</th><th>狀態</th><th class="num">執行時間</th><th class="num">執行率</th>
                    <th class="num">CPU 平均/最高</th><th class="num">記憶體 平均/最高 MB</th>
                    <th class="num">CPU 秒數</th><th class="num">磁碟峰值</th><th class="num">網路峰值</th><th class="num">區間/重啟</th>
                </tr></thead>
                <tbody>${rows}</tbody>
            </table>
        </div>`;
    };

    const diagnostics = session => {
        const all = session.diagnostics || [];
        const main = all.filter(diagnostic => diagnostic.severity !== "Info");
        const info = all.filter(diagnostic => diagnostic.severity === "Info");

        const renderItems = items => items.map(diagnostic => `
            <div class="diag ${diagnostic.severity.toLowerCase()}">
                <div class="dot"></div>
                <div class="code">${esc(diagnostic.code)}</div>
                <div class="msg">${esc(diagnostic.message)}</div>
            </div>`).join("");

        return `
            <div class="diag-list">${main.length ? renderItems(main) : `<div class="empty">沒有警告或錯誤</div>`}</div>
            ${info.length ? `<details style="margin-top:8px"><summary>資訊（${info.length}）</summary><div class="diag-list">${renderItems(info)}</div></details>` : ""}`;
    };

    // ---------------------------------------------------------------- compare

    const renderCompareView = () => `
        <div class="card no-print">
            <div class="toolbar" style="margin:0">
                <div class="segmented">
                    <button data-action="compare-mode" data-mode="same" class="${state.compareMode === "same" ? "active" : ""}" type="button">同機多次比較</button>
                    <button data-action="compare-mode" data-mode="cross" class="${state.compareMode === "cross" ? "active" : ""}" type="button">跨機比較</button>
                </div>
                <span class="spacer"></span>
                <span class="note">${state.compareMode === "same" ? "同一裝置的多次測試" : "不同裝置之間的比較"}</span>
            </div>
        </div>
        ${state.compareMode === "same" ? renderSameCompare() : renderCrossCompare()}`;

    const sameDeviceSessions = () => SESSIONS_BY_DEVICE.get(state.compareDeviceKey) || [];

    const sameCompareSessions = () => sameDeviceSessions()
        .filter(session => state.sameSelected.has(session.sessionKey))
        .sort((a, b) => new Date(a.startTime) - new Date(b.startTime));

    const ensureSameCompareState = (force = false) => {
        if (!state.compareDeviceKey || !SESSIONS_BY_DEVICE.has(state.compareDeviceKey)) {
            state.compareDeviceKey = DEVICES
                .slice()
                .sort((a, b) => b.sessionCount - a.sessionCount)[0]?.deviceKey || null;
        }
        if (!state.compareDeviceKey) return;
        const list = sameDeviceSessions();
        if (!list.length) return;
        const deviceBaseline = list.find(session => session.isBaseline)?.sessionKey || list[list.length - 1].sessionKey;

        if (force || !state.sameSelectionInitialized) {
            state.sameBaseline = deviceBaseline;
            state.sameSelected.clear();
            state.sameSelected.add(deviceBaseline);
            list.slice().reverse()
                .filter(session => session.sessionKey !== deviceBaseline)
                .slice(0, 2)
                .forEach(session => state.sameSelected.add(session.sessionKey));
            state.sameSelectionInitialized = true;
            return;
        }

        if (!state.sameBaseline || !list.some(session => session.sessionKey === state.sameBaseline)) {
            state.sameBaseline = state.sameSelected.size
                ? Array.from(state.sameSelected)[0]
                : deviceBaseline;
        }
    };

    const renderSameCompare = () => {
        ensureSameCompareState();
        const sessions = sameDeviceSessions();
        const device = DEVICE_MAP.get(state.compareDeviceKey);
        if (!sessions.length) return `<div class="empty">此裝置沒有場次</div>`;

        const baseline = SESSION_MAP.get(state.sameBaseline) || null;
        const selected = sameCompareSessions();
        const options = sessions.map(session => `<option value="${esc(session.sessionKey)}" ${session.sessionKey === state.sameBaseline ? "selected" : ""}>${esc(session.folderName)} ・ ${dateTime(session.startTime)}</option>`).join("");
        const deviceOptions = DEVICES.map(item => `<option value="${esc(item.deviceKey)}" ${item.deviceKey === state.compareDeviceKey ? "selected" : ""}>${esc(item.displayName)}（${item.sessionCount} 場）</option>`).join("");

        const checkList = sessions.slice().sort((a, b) => new Date(b.startTime) - new Date(a.startTime)).map(session => `
            <label class="check-item">
                <input type="checkbox" data-role="same-select" data-key="${esc(session.sessionKey)}" ${state.sameSelected.has(session.sessionKey) ? "checked" : ""}>
                <span class="name">${esc(session.folderName)}${session.isBaseline ? " ★" : ""}</span>
                <span class="device">${shortDate(session.startTime)}</span>
            </label>`).join("");

        return `
            <div class="card section">
                <div class="toolbar">
                    <label>裝置 <select data-role="compare-device">${deviceOptions}</select></label>
                    <label>基準 <select data-role="compare-baseline">${options}</select></label>
                    <button class="btn small" data-action="select-all" type="button">全選</button>
                    <button class="btn small" data-action="clear-selection" type="button">清除</button>
                    <span class="spacer"></span>
                    <span class="note">已選 ${selected.length} 場</span>
                </div>
                <div class="compare-layout">
                    <div class="card" style="box-shadow:none;background:var(--panel-2)">
                        <div class="card-title"><h2>選擇場次</h2><span class="sub">${esc(device?.displayName || "")}</span></div>
                        <div class="check-list">${checkList}</div>
                    </div>
                    <div>
                        <div class="card">
                            <div class="card-title"><h2>比較結果</h2><span class="sub">${baseline ? `基準：${esc(baseline.folderName)}` : "尚未選擇基準"}</span></div>
                            ${compareTable(selected, baseline)}
                        </div>
                        <div class="card section">
                            <div class="card-title">
                                <h2>時間序列疊圖</h2>
                                <select data-role="overlay-metric">
                                    <option value="cpuTotal" ${state.overlayMetric === "cpuTotal" ? "selected" : ""}>全系統 CPU</option>
                                    <option value="memoryUsed" ${state.overlayMetric === "memoryUsed" ? "selected" : ""}>已用記憶體</option>
                                    <option value="cpuSimulator" ${state.overlayMetric === "cpuSimulator" ? "selected" : ""}>模擬器 CPU</option>
                                </select>
                            </div>
                            <div id="chart-compare-overlay" class="chart tall"></div>
                        </div>
                    </div>
                </div>
            </div>`;
    };

    const compareTable = (sessions, baseline) => {
        if (!sessions.length) return `<div class="empty">請勾選至少一場</div>`;

        const rows = sessions.map(session => {
            const isBaseline = baseline && baseline.sessionKey === session.sessionKey;
            const cpuDelta = deltaAgainst(session, baseline, s => s.metrics.cpuAvg);
            const ramDelta = deltaAgainst(session, baseline, s => s.metrics.ramAvg);
            const recognitionDelta = deltaAgainst(session, baseline, s => s.metrics.recognitionAvg);

            return `<tr>
                <td>${esc(session.folderName)}${isBaseline ? " ★" : ""}</td>
                <td class="nowrap">${esc(session.deviceDisplay)}</td>
                <td class="nowrap">${dateTime(session.startTime)}</td>
                <td class="num">${num(session.metrics.cpuAvg)}</td>
                <td class="num ${isBaseline ? "delta-neutral" : deltaClass(cpuDelta)}">${isBaseline ? "基準" : signed(cpuDelta, 1, "%")}</td>
                <td class="num">${num(session.metrics.cpuMax)}</td>
                <td class="num">${num(session.metrics.ramAvg, 2)}</td>
                <td class="num ${isBaseline ? "delta-neutral" : deltaClass(ramDelta)}">${isBaseline ? "基準" : signed(ramDelta, 2, " GB")}</td>
                <td class="num">${num(session.metrics.ramMax, 2)}</td>
                <td class="num">${rate(session.metrics.diskPeak, "KB/s")}</td>
                <td class="num">${rate(session.metrics.networkPeak, "Kbps")}</td>
                <td class="num">${ms(session.metrics.recognitionAvg)}</td>
                <td class="num ${isBaseline ? "delta-neutral" : deltaClass(recognitionDelta)}">${isBaseline ? "基準" : signed(recognitionDelta, 0, " ms")}</td>
                <td class="num">${pct(session.metrics.completeness * 100, 0)}</td>
                <td class="num">${session.warningCount + session.errorCount}</td>
            </tr>`;
        }).join("");

        return `<div class="table-wrap tall">
            <table>
                <thead><tr>
                    <th>場次</th><th>裝置</th><th>開始</th><th class="num">CPU 平均</th><th class="num">ΔCPU</th><th class="num">CPU 最高</th>
                    <th class="num">RAM 平均</th><th class="num">ΔRAM</th><th class="num">RAM 最高</th>
                    <th class="num">磁碟</th><th class="num">網路</th><th class="num">辨識</th><th class="num">Δ辨識</th>
                    <th class="num">完整率</th><th class="num">警告</th>
                </tr></thead>
                <tbody>${rows}</tbody>
            </table>
        </div>`;
    };

    const crossCompareSessions = () => DEVICES
        .map(device => SESSION_MAP.get(state.crossSelection[device.deviceKey]))
        .filter(Boolean);

    const ensureCrossState = () => {
        if (!Object.keys(state.crossSelection).length) {
            DEVICES.forEach(device => {
                const list = SESSIONS_BY_DEVICE.get(device.deviceKey) || [];
                if (list.length) state.crossSelection[device.deviceKey] = list[list.length - 1].sessionKey;
            });
        }
        if (!state.crossBaseline || !SESSION_MAP.has(state.crossBaseline)) {
            state.crossBaseline = crossCompareSessions()[0]?.sessionKey || null;
        }
    };

    const renderCrossCompare = () => {
        ensureCrossState();
        const selected = crossCompareSessions();
        if (!selected.some(session => session.sessionKey === state.crossBaseline)) {
            state.crossBaseline = selected[0]?.sessionKey || null;
        }
        const baseline = SESSION_MAP.get(state.crossBaseline) || null;
        const selectors = DEVICES.map(device => {
            const list = SESSIONS_BY_DEVICE.get(device.deviceKey) || [];
            const options = list.slice().sort((a, b) => new Date(b.startTime) - new Date(a.startTime)).map(session => `<option value="${esc(session.sessionKey)}" ${state.crossSelection[device.deviceKey] === session.sessionKey ? "selected" : ""}>${esc(session.folderName)} ・ ${dateTime(session.startTime)}</option>`).join("");
            return `<label class="field" style="align-items:flex-start;flex-direction:column;gap:4px">
                <span>${esc(device.displayName)}</span>
                <select data-role="cross-device" data-device="${esc(device.deviceKey)}" style="min-width:230px">${options}</select>
            </label>`;
        }).join("");

        const baselineOptions = selected.map(session => `<option value="${esc(session.sessionKey)}" ${session.sessionKey === state.crossBaseline ? "selected" : ""}>${esc(session.folderName)} ・ ${esc(session.deviceDisplay)}</option>`).join("");
        const hardwareWarning = hasHardwareDifference(selected);

        return `
            <div class="card section">
                <div class="card-title"><h2>跨機選擇</h2><span class="sub">每台裝置選一場</span></div>
                <div class="grid three">${selectors}</div>
                <div class="toolbar" style="margin-top:12px;margin-bottom:0">
                    <label>基準 <select data-role="cross-baseline">${baselineOptions}</select></label>
                    <span class="spacer"></span>
                    ${hardwareWarning ? `<span class="badge amber">硬體規格不同，數值僅供參考</span>` : `<span class="badge teal">硬體規格相近</span>`}
                </div>
            </div>

            <div class="card section">
                <div class="card-title"><h2>硬體規格</h2><span class="sub">跨機比較前請先確認</span></div>
                ${hardwareTable(selected)}
            </div>

            <div class="card section">
                <div class="card-title"><h2>比較結果</h2><span class="sub">${baseline ? `基準：${esc(baseline.folderName)}` : "尚未選擇基準"}</span></div>
                ${compareTable(selected, baseline)}
            </div>

            <div class="card section">
                <div class="card-title">
                    <h2>時間序列疊圖</h2>
                    <select data-role="overlay-metric">
                        <option value="cpuTotal" ${state.overlayMetric === "cpuTotal" ? "selected" : ""}>全系統 CPU</option>
                        <option value="memoryUsed" ${state.overlayMetric === "memoryUsed" ? "selected" : ""}>已用記憶體</option>
                        <option value="cpuSimulator" ${state.overlayMetric === "cpuSimulator" ? "selected" : ""}>模擬器 CPU</option>
                    </select>
                </div>
                <div id="chart-cross-overlay" class="chart tall"></div>
            </div>`;
    };

    const hasHardwareDifference = sessions => {
        const signatures = new Set(sessions.map(session => {
            const device = DEVICE_MAP.get(session.deviceKey);
            return `${device?.cpuModel || ""}|${device?.cores || ""}|${device?.ramGb || ""}|${device?.osBuild || ""}`;
        }));
        return signatures.size > 1;
    };

    const hardwareTable = sessions => {
        if (!sessions.length) return `<div class="empty">沒有選擇場次</div>`;
        const rows = sessions.map(session => {
            const device = DEVICE_MAP.get(session.deviceKey);
            return `<tr>
                <td>${esc(device?.displayName || session.deviceDisplay)}</td>
                <td>${esc(device?.cpuModel || "—")}</td>
                <td class="num">${device?.cores ?? "—"}</td>
                <td class="num">${device?.ramGb ? `${num(device.ramGb, 1)} GB` : "—"}</td>
                <td>${esc(device?.osName || "—")} ${device?.osBuild ? `(${esc(device.osBuild)})` : ""}</td>
                <td>${esc(device?.gpu || "—")}</td>
            </tr>`;
        }).join("");

        return `<div class="table-wrap">
            <table>
                <thead><tr><th>裝置</th><th>CPU</th><th class="num">核心</th><th class="num">RAM</th><th>OS</th><th>GPU</th></tr></thead>
                <tbody>${rows}</tbody>
            </table>
        </div>`;
    };

    // ---------------------------------------------------------------- events

    const renderDeviceSelect = () => {
        const select = $("#device-select");
        if (!select) return;
        const options = [`<option value="all">全部裝置（${SESSIONS.length} 場）</option>`]
            .concat(DEVICES.map(device => `<option value="${esc(device.deviceKey)}">${esc(device.displayName)}（${device.sessionCount} 場）</option>`))
            .join("");
        select.innerHTML = options;
        select.value = DEVICES.some(device => device.deviceKey === state.deviceKey) ? state.deviceKey : "all";
    };

    const selectView = view => {
        state.view = view;
        if (view === "session") {
            const sessions = filteredSessions();
            if (!sessions.some(session => session.sessionKey === state.sessionKey)) {
                state.sessionKey = latestSession(sessions)?.sessionKey || null;
            }
        }
        render();
    };

    const bindStaticEvents = () => {
        $("#device-select").addEventListener("change", event => {
            state.deviceKey = event.target.value;
            if (state.view === "session") {
                state.sessionKey = latestSession(filteredSessions())?.sessionKey || null;
            }
            render();
        });

        $("#theme-btn").addEventListener("click", () => {
            state.theme = state.theme === "dark" ? "light" : "dark";
            render();
        });

        $("#print-btn").addEventListener("click", () => window.print());

        $("#tabs").addEventListener("click", event => {
            const button = event.target.closest("button[data-view]");
            if (button) selectView(button.dataset.view);
        });

        $("#main").addEventListener("click", event => {
            const action = event.target.closest("[data-action]");
            if (action) {
                const name = action.dataset.action;
                if (name === "open-session") {
                    state.sessionKey = action.dataset.key;
                    state.view = "session";
                } else if (name === "view") {
                    state.view = action.dataset.view;
                } else if (name === "all-devices") {
                    state.deviceKey = "all";
                } else if (name === "compare-mode") {
                    state.compareMode = action.dataset.mode;
                } else if (name === "select-all") {
                    sameDeviceSessions().forEach(session => state.sameSelected.add(session.sessionKey));
                } else if (name === "clear-selection") {
                    state.sameSelected.clear();
                }
                render();
                return;
            }

            const row = event.target.closest("tr[data-action='open-session']");
            if (row) {
                state.sessionKey = row.dataset.key;
                state.view = "session";
                render();
            }
        });

        $("#main").addEventListener("change", event => {
            const target = event.target;
            const role = target.dataset?.role;
            if (!role) return;

            if (role === "session-select") {
                state.sessionKey = target.value;
            } else if (role === "compare-device") {
                state.compareDeviceKey = target.value;
                state.sameSelected.clear();
                state.sameBaseline = null;
                state.sameSelectionInitialized = false;
                ensureSameCompareState(true);
            } else if (role === "compare-baseline") {
                state.sameBaseline = target.value;
            } else if (role === "cross-device") {
                state.crossSelection[target.dataset.device] = target.value;
                if (!crossCompareSessions().some(session => session.sessionKey === state.crossBaseline)) {
                    state.crossBaseline = crossCompareSessions()[0]?.sessionKey || null;
                }
            } else if (role === "cross-baseline") {
                state.crossBaseline = target.value;
            } else if (role === "overlay-metric") {
                state.overlayMetric = target.value;
            } else if (role === "same-select") {
                if (target.checked) state.sameSelected.add(target.dataset.key);
                else state.sameSelected.delete(target.dataset.key);
                if (!state.sameSelected.has(state.sameBaseline) && state.sameSelected.size) {
                    state.sameBaseline = Array.from(state.sameSelected)[0];
                }
            }

            render();
        });

        window.addEventListener("resize", () => {
            state.charts.forEach(chart => {
                try { chart.resize(); } catch { /* ignore */ }
            });
        });

        window.addEventListener("hashchange", () => {
            applyHash();
            render();
        });
    };

    // ---------------------------------------------------------------- hash

    const updateHash = () => {
        const params = new URLSearchParams();
        params.set("view", state.view);
        if (state.deviceKey !== "all") params.set("device", state.deviceKey);
        if (state.view === "session" && state.sessionKey) params.set("session", state.sessionKey);
        if (state.view === "compare") {
            params.set("mode", state.compareMode);
            if (state.compareMode === "same" && state.compareDeviceKey) params.set("deviceCompare", state.compareDeviceKey);
            if (state.compareMode === "cross" && state.crossBaseline) params.set("baseline", state.crossBaseline);
        }
        try {
            history.replaceState(null, "", `#${params.toString()}`);
        } catch {
            // file:// in some browsers may reject history changes; the report still works.
        }
    };
    const applyHash = () => {
        const params = new URLSearchParams(location.hash.replace(/^#/, ""));
        const view = params.get("view");
        if (["overview", "session", "compare"].includes(view)) state.view = view;
        const device = params.get("device");
        if (device && (device === "all" || DEVICE_MAP.has(device))) state.deviceKey = device;
        const session = params.get("session");
        if (session && SESSION_MAP.has(session)) state.sessionKey = session;
        const mode = params.get("mode");
        if (mode === "same" || mode === "cross") state.compareMode = mode;
        const deviceCompare = params.get("deviceCompare");
        if (deviceCompare && SESSIONS_BY_DEVICE.has(deviceCompare)) state.compareDeviceKey = deviceCompare;
        const baseline = params.get("baseline");
        if (baseline && SESSION_MAP.has(baseline)) state.crossBaseline = baseline;
    };

    // ---------------------------------------------------------------- boot

    const boot = () => {
        $("#source-path").textContent = DATA.sourcePath || "";
        state.sessionKey = latestSession(SESSIONS)?.sessionKey || null;
        state.compareDeviceKey = DEVICES
            .slice()
            .sort((a, b) => b.sessionCount - a.sessionCount)[0]?.deviceKey || null;
        ensureSameCompareState();
        ensureCrossState();
        applyHash();
        bindStaticEvents();
        render();
    };

    boot();
})();
