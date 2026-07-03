(function () {
    document.addEventListener("DOMContentLoaded", function () {
        initAos();
        initHomeHero();
        initUpload();
    });

    function initAos() {
        if (!window.AOS) {
            return;
        }

        window.AOS.init({
            duration: 650,
            easing: "ease-out-cubic",
            once: true,
            offset: 70
        });
    }

    function initHomeHero() {
        var hero = document.querySelector(".hero-section");
        if (!hero) {
            return;
        }

        if (window.gsap) {
            window.gsap.from(".hero-copy-block > *", {
                opacity: 0,
                y: 18,
                duration: 0.75,
                stagger: 0.08,
                ease: "power3.out"
            });

            window.gsap.to(".hero-gradient", {
                backgroundPosition: "100% 50%",
                duration: 12,
                repeat: -1,
                yoyo: true,
                ease: "sine.inOut"
            });
        }

        var canvas = document.getElementById("heroPreviewChart");
        if (!canvas || !window.Chart) {
            return;
        }

        var context = canvas.getContext("2d");
        var gradient = context.createLinearGradient(0, 0, 0, 260);
        gradient.addColorStop(0, "rgba(66, 216, 140, 0.42)");
        gradient.addColorStop(1, "rgba(66, 216, 140, 0)");

        new window.Chart(canvas, {
            type: "line",
            data: {
                labels: ["Apr 6", "Apr 13", "Apr 20", "Apr 27", "May 4", "May 11", "May 18", "May 25"],
                datasets: [{
                    label: "Balance",
                    data: [42000, 51000, 47500, 63800, 68400, 74200, 80100, 84300],
                    borderColor: "#42d88c",
                    backgroundColor: gradient,
                    borderWidth: 3,
                    pointRadius: 0,
                    tension: 0.35,
                    fill: true
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: {
                    duration: 1200,
                    easing: "easeOutQuart"
                },
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        enabled: false
                    }
                },
                scales: {
                    x: {
                        grid: {
                            color: "rgba(255,255,255,0.06)"
                        },
                        ticks: {
                            color: "#9fb0aa"
                        }
                    },
                    y: {
                        grid: {
                            color: "rgba(255,255,255,0.06)"
                        },
                        ticks: {
                            color: "#9fb0aa",
                            callback: function (value) {
                                return "$" + Math.round(value / 1000) + "k";
                            }
                        }
                    }
                }
            }
        });
    }

    function initUpload() {
        var form = document.getElementById("uploadForm");
        var dropZone = document.getElementById("dropZone");
        var fileInput = document.getElementById("csvFile");
        var fileMeta = document.getElementById("fileMeta");
        var uploadButton = document.getElementById("uploadButton");
        var progress = document.getElementById("uploadProgress");

        if (!form || !dropZone || !fileInput) {
            return;
        }

        ["dragenter", "dragover"].forEach(function (eventName) {
            dropZone.addEventListener(eventName, function (event) {
                event.preventDefault();
                dropZone.classList.add("is-dragging");
            });
        });

        ["dragleave", "drop"].forEach(function (eventName) {
            dropZone.addEventListener(eventName, function (event) {
                event.preventDefault();
                dropZone.classList.remove("is-dragging");
            });
        });

        dropZone.addEventListener("drop", function (event) {
            var file = event.dataTransfer && event.dataTransfer.files ? event.dataTransfer.files[0] : null;
            if (!file) {
                return;
            }

            if (window.DataTransfer) {
                var transfer = new DataTransfer();
                transfer.items.add(file);
                fileInput.files = transfer.files;
            }

            handleFile(file);
        });

        fileInput.addEventListener("change", function () {
            var file = fileInput.files && fileInput.files[0] ? fileInput.files[0] : null;
            handleFile(file);
        });

        form.addEventListener("submit", function () {
            if (!progress) {
                return;
            }

            progress.classList.remove("d-none");
            uploadButton.disabled = true;
            var bar = progress.querySelector(".progress-bar");
            var current = 0;

            var timer = window.setInterval(function () {
                current = Math.min(current + 13, 92);
                bar.style.width = current + "%";
                progress.setAttribute("aria-valuenow", String(current));

                if (current >= 92) {
                    window.clearInterval(timer);
                }
            }, 160);
        });

        function handleFile(file) {
            if (!file) {
                uploadButton.disabled = true;
                fileMeta.classList.add("d-none");
                return;
            }

            uploadButton.disabled = false;
            fileMeta.classList.remove("d-none");
            fileMeta.textContent = file.name + " - " + formatBytes(file.size);
            previewCsv(file);
        }
    }

    function previewCsv(file) {
        var previewEmpty = document.getElementById("previewEmpty");
        var previewWrap = document.getElementById("previewTableWrap");
        var previewHead = document.getElementById("previewHead");
        var previewBody = document.getElementById("previewBody");

        if (!previewEmpty || !previewWrap || !previewHead || !previewBody) {
            return;
        }

        var reader = new FileReader();

        reader.onload = function () {
            var text = String(reader.result || "");
            var rows = text
                .split(/\r?\n/)
                .filter(function (line) {
                    return line.trim().length > 0;
                })
                .slice(0, 7)
                .map(parseCsvLine);

            previewHead.replaceChildren();
            previewBody.replaceChildren();

            if (rows.length === 0) {
                previewEmpty.classList.remove("d-none");
                previewWrap.classList.add("d-none");
                return;
            }

            var headerRow = document.createElement("tr");
            rows[0].forEach(function (header) {
                var th = document.createElement("th");
                th.scope = "col";
                th.textContent = header;
                headerRow.appendChild(th);
            });
            previewHead.appendChild(headerRow);

            rows.slice(1).forEach(function (row) {
                var tr = document.createElement("tr");
                rows[0].forEach(function (_, index) {
                    var td = document.createElement("td");
                    td.textContent = row[index] || "";
                    tr.appendChild(td);
                });
                previewBody.appendChild(tr);
            });

            previewEmpty.classList.add("d-none");
            previewWrap.classList.remove("d-none");
        };

        reader.readAsText(file);
    }

    function parseCsvLine(line) {
        var cells = [];
        var current = "";
        var insideQuotes = false;

        for (var index = 0; index < line.length; index++) {
            var char = line[index];
            var next = line[index + 1];

            if (char === "\"" && insideQuotes && next === "\"") {
                current += "\"";
                index++;
                continue;
            }

            if (char === "\"") {
                insideQuotes = !insideQuotes;
                continue;
            }

            if (char === "," && !insideQuotes) {
                cells.push(current.trim());
                current = "";
                continue;
            }

            current += char;
        }

        cells.push(current.trim());
        return cells;
    }

    function formatBytes(bytes) {
        if (bytes < 1024) {
            return bytes + " B";
        }

        if (bytes < 1024 * 1024) {
            return (bytes / 1024).toFixed(1) + " KB";
        }

        return (bytes / (1024 * 1024)).toFixed(1) + " MB";
    }
})();
