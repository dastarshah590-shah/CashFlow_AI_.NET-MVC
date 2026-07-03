(function () {
    document.addEventListener("DOMContentLoaded", initDashboard);

    var currencyFormatter = new Intl.NumberFormat("en-US", {
        style: "currency",
        currency: "USD",
        maximumFractionDigits: 0
    });

    function initDashboard() {
        var root = document.querySelector("[data-dashboard]");
        if (!root) {
            return;
        }

        if (root.dataset.hasData !== "true") {
            return;
        }

        loadForecast(root.dataset.dataUrl);
    }

    async function loadForecast(url) {
        var skeleton = document.getElementById("dashboardSkeleton");
        var content = document.getElementById("dashboardContent");
        var error = document.getElementById("dashboardError");

        try {
            var response = await fetch(url, {
                headers: {
                    "Accept": "application/json"
                }
            });

            if (!response.ok) {
                throw new Error("Unable to load the forecast.");
            }

            var data = await response.json();
            renderForecast(data);

            skeleton.classList.add("d-none");
            content.classList.remove("d-none");
            error.classList.add("d-none");

            if (window.AOS) {
                window.AOS.refresh();
            }
        } catch (exception) {
            skeleton.classList.add("d-none");
            error.textContent = exception.message || "Unable to load the forecast.";
            error.classList.remove("d-none");
        }
    }

    function renderForecast(data) {
        animateCurrency(document.getElementById("currentBalance"), Number(data.currentBalance || 0));
        animateCurrency(document.getElementById("projectedBalance"), Number(data.projectedBalance30d || 0));
        animateCurrency(document.getElementById("burnRate"), Number(data.burnRate || 0));
        renderRiskBadge(data.riskLevel);
        renderChart(data);
        typeText(document.getElementById("aiInsight"), data.aiInsight || "No insight was generated.");
    }

    function renderRiskBadge(level) {
        var badge = document.getElementById("riskBadge");
        var normalized = String(level || "green").toLowerCase();
        var labels = {
            green: "Healthy",
            yellow: "Watch",
            red: "High risk"
        };

        badge.className = "risk-badge risk-" + normalized;
        badge.textContent = labels[normalized] || "Healthy";
    }

    function renderChart(data) {
        var canvas = document.getElementById("cashFlowChart");
        if (!canvas || !window.Chart) {
            return;
        }

        var historical = data.historicalWeeks || [];
        var projected = data.projectedWeeks || [];
        var allWeeks = historical.concat(projected);
        var historyLength = historical.length;
        var labels = allWeeks.map(function (week) {
            return week.weekLabel;
        });

        var historicalSeries = allWeeks.map(function (week, index) {
            return index < historyLength ? Number(week.endingBalance) : null;
        });

        var projectedSeries = allWeeks.map(function (week, index) {
            if (historyLength > 0 && index < historyLength - 1) {
                return null;
            }

            return Number(week.endingBalance);
        });

        var balances = allWeeks.map(function (week) {
            return Number(week.endingBalance);
        });
        var minBalance = balances.length ? Math.min.apply(Math, balances) : 0;
        var maxBalance = balances.length ? Math.max.apply(Math, balances) : 0;
        var padding = Math.max((maxBalance - minBalance) * 0.18, 1000);

        var context = canvas.getContext("2d");
        var actualGradient = context.createLinearGradient(0, 0, 0, 420);
        actualGradient.addColorStop(0, "rgba(66, 216, 140, 0.34)");
        actualGradient.addColorStop(1, "rgba(66, 216, 140, 0)");

        var forecastGradient = context.createLinearGradient(0, 0, 0, 420);
        forecastGradient.addColorStop(0, "rgba(75, 199, 231, 0.28)");
        forecastGradient.addColorStop(1, "rgba(75, 199, 231, 0)");

        var dangerZonePlugin = {
            id: "dangerZone",
            beforeDatasetsDraw: function (chart) {
                var chartArea = chart.chartArea;
                var yScale = chart.scales.y;
                var yZero = yScale.getPixelForValue(0);

                if (yZero >= chartArea.bottom) {
                    return;
                }

                var top = Math.max(chartArea.top, yZero);
                var height = chartArea.bottom - top;
                var ctx = chart.ctx;

                ctx.save();
                ctx.fillStyle = "rgba(255, 94, 94, 0.09)";
                ctx.fillRect(chartArea.left, top, chartArea.right - chartArea.left, height);
                ctx.restore();
            }
        };

        new window.Chart(canvas, {
            type: "line",
            data: {
                labels: labels,
                datasets: [
                    {
                        label: "Historical balance",
                        data: historicalSeries,
                        borderColor: "#42d88c",
                        backgroundColor: actualGradient,
                        borderWidth: 3,
                        pointRadius: 3,
                        pointHoverRadius: 5,
                        tension: 0.35,
                        spanGaps: false,
                        fill: true
                    },
                    {
                        label: "Forecasted balance",
                        data: projectedSeries,
                        borderColor: "#4bc7e7",
                        backgroundColor: forecastGradient,
                        borderDash: [7, 5],
                        borderWidth: 3,
                        pointRadius: 3,
                        pointHoverRadius: 5,
                        tension: 0.35,
                        spanGaps: true,
                        fill: true
                    }
                ]
            },
            plugins: [dangerZonePlugin],
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: {
                    duration: 1400,
                    easing: "easeOutQuart"
                },
                interaction: {
                    mode: "index",
                    intersect: false
                },
                plugins: {
                    legend: {
                        labels: {
                            color: "#eef8f3",
                            usePointStyle: true,
                            boxWidth: 8
                        }
                    },
                    tooltip: {
                        backgroundColor: "rgba(5, 12, 11, 0.94)",
                        borderColor: "rgba(204, 230, 218, 0.16)",
                        borderWidth: 1,
                        callbacks: {
                            label: function (context) {
                                return context.dataset.label + ": " + formatCurrency(context.parsed.y);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            color: "rgba(255,255,255,0.05)"
                        },
                        ticks: {
                            color: "#9fb0aa",
                            maxRotation: 0,
                            autoSkip: true,
                            autoSkipPadding: 18
                        }
                    },
                    y: {
                        suggestedMin: Math.min(0, minBalance - padding),
                        suggestedMax: maxBalance + padding,
                        grid: {
                            color: function (context) {
                                return context.tick.value === 0 ? "rgba(255, 94, 94, 0.55)" : "rgba(255,255,255,0.06)";
                            }
                        },
                        ticks: {
                            color: "#9fb0aa",
                            callback: function (value) {
                                return formatCurrency(value);
                            }
                        }
                    }
                }
            }
        });
    }

    function animateCurrency(element, target) {
        if (!element) {
            return;
        }

        var start = 0;
        var duration = 900;
        var startedAt = performance.now();

        function step(now) {
            var progress = Math.min((now - startedAt) / duration, 1);
            var eased = 1 - Math.pow(1 - progress, 3);
            var value = start + (target - start) * eased;
            element.textContent = formatCurrency(value);

            if (progress < 1) {
                requestAnimationFrame(step);
            }
        }

        requestAnimationFrame(step);
    }

    function typeText(element, text) {
        if (!element) {
            return;
        }

        element.textContent = "";
        var index = 0;
        var delay = Math.max(8, Math.min(24, 1500 / Math.max(text.length, 1)));

        function tick() {
            element.textContent += text[index] || "";
            index++;

            if (index < text.length) {
                window.setTimeout(tick, delay);
            }
        }

        tick();
    }

    function formatCurrency(value) {
        return currencyFormatter.format(Number(value || 0));
    }
})();
