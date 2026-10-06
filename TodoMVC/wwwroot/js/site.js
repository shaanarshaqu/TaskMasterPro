// Web Audio API Chime synthesizer for start-time alerts
function playStartChime() {
    try {
        const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        
        // First tone
        const osc1 = audioCtx.createOscillator();
        const gain1 = audioCtx.createGain();
        osc1.type = 'sine';
        osc1.frequency.setValueAtTime(587.33, audioCtx.currentTime); // D5
        gain1.gain.setValueAtTime(0.25, audioCtx.currentTime);
        gain1.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + 0.6);
        osc1.connect(gain1);
        gain1.connect(audioCtx.destination);
        osc1.start();
        osc1.stop(audioCtx.currentTime + 0.6);

        // Second tone harmonic
        setTimeout(() => {
            const osc2 = audioCtx.createOscillator();
            const gain2 = audioCtx.createGain();
            osc2.type = 'sine';
            osc2.frequency.setValueAtTime(880, audioCtx.currentTime); // A5
            gain2.gain.setValueAtTime(0.3, audioCtx.currentTime);
            gain2.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + 0.8);
            osc2.connect(gain2);
            gain2.connect(audioCtx.destination);
            osc2.start();
            osc2.stop(audioCtx.currentTime + 0.8);
        }, 150);
    } catch (e) {
        console.warn("Audio play prevented or unsupported", e);
    }
}

// Notification store
let alertNotifications = [];

function addNotificationToList(alertData) {
    alertNotifications.unshift(alertData);
    updateNotificationBellUI();
    showLiveToast(alertData);
}

function updateNotificationBellUI() {
    const badge = document.getElementById("notifBadge");
    const countSpan = document.getElementById("notifCount");
    const listContainer = document.getElementById("notifItemsList");

    if (badge && countSpan) {
        if (alertNotifications.length > 0) {
            badge.style.display = "block";
            countSpan.textContent = alertNotifications.length;
        } else {
            badge.style.display = "none";
            countSpan.textContent = "0";
        }
    }

    if (listContainer) {
        if (alertNotifications.length === 0) {
            listContainer.innerHTML = '<div class="p-3 text-center text-muted small"><i class="bi bi-bell-slash me-1"></i> No new start-time alerts</div>';
        } else {
            listContainer.innerHTML = alertNotifications.map(item => `
                <div class="notif-item">
                    <div class="stat-icon" style="background-color: #ECFDF5; color: #059669; width: 34px; height: 34px; font-size: 1rem;">
                        <i class="bi bi-alarm-fill"></i>
                    </div>
                    <div class="flex-grow-1">
                        <div class="d-flex justify-content-between align-items-center">
                            <strong class="small text-dark">${item.title}</strong>
                            <span class="badge bg-light text-dark border" style="font-size: 0.65rem;">${item.category || 'General'}</span>
                        </div>
                        <div class="text-muted" style="font-size: 0.75rem;">Scheduled: ${item.startTime}</div>
                        <div class="mt-1">
                            <button class="btn btn-sm btn-outline-success py-0 px-2" style="font-size: 0.7rem;" onclick="quickUpdateStatus(${item.id}, 'InProgress')">
                                Start Working
                            </button>
                        </div>
                    </div>
                </div>
            `).join('');
        }
    }
}

function showLiveToast(alertData) {
    playStartChime();

    const toastContainer = document.getElementById("liveToastContainer");
    if (!toastContainer) return;

    const toastId = "toast_" + Date.now();
    const toastHtml = `
        <div id="${toastId}" class="live-alert-card">
            <div class="d-flex align-items-start justify-content-between mb-2">
                <div class="d-flex align-items-center gap-2">
                    <span class="badge bg-emerald-500 text-white" style="background: #10B981; font-size: 0.75rem;">
                        <i class="bi bi-clock-history me-1"></i> TIME TO WORK
                    </span>
                    <span class="text-secondary small" style="color: #94A3B8 !important;">${alertData.startTime}</span>
                </div>
                <button type="button" class="btn-close btn-close-white btn-sm" onclick="document.getElementById('${toastId}').remove()"></button>
            </div>
            <h6 class="text-white mb-1 fw-bold">${alertData.title}</h6>
            <p class="text-light small mb-3" style="color: #CBD5E1 !important; font-size: 0.825rem;">
                ${alertData.description || 'This task is scheduled to start right now. Ready to begin?'}
            </p>
            <div class="d-flex gap-2">
                <button class="btn btn-sm btn-success px-3 fw-semibold" onclick="quickUpdateStatus(${alertData.id}, 'InProgress'); document.getElementById('${toastId}').remove();">
                    <i class="bi bi-play-circle-fill me-1"></i> Mark In-Progress
                </button>
                <button class="btn btn-sm btn-outline-light px-2" onclick="document.getElementById('${toastId}').remove();">
                    Dismiss
                </button>
            </div>
        </div>
    `;

    toastContainer.insertAdjacentHTML('beforeend', toastHtml);

    // Auto dismiss toast after 20 seconds
    setTimeout(() => {
        const el = document.getElementById(toastId);
        if (el) el.remove();
    }, 20000);
}

// SignalR initialization
function initSignalR() {
    if (typeof signalR === "undefined") {
        console.warn("SignalR library not loaded yet.");
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/todoNotifications")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveTodoStartAlert", function (alertData) {
        console.log("Real-time Todo Alert received:", alertData);
        addNotificationToList(alertData);
    });

    connection.start()
        .then(function () {
            console.log("SignalR connected successfully.");
        })
        .catch(function (err) {
            console.error("SignalR connection error: ", err.toString());
        });
}

// Quick status update AJAX
async function quickUpdateStatus(todoId, newStatus) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    try {
        const response = await fetch(`/Todos/UpdateStatus?id=${todoId}&status=${newStatus}`, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token,
                'Content-Type': 'application/json'
            }
        });

        const data = await response.json();
        if (data.success) {
            // Reload to reflect new status if on todos page, or show banner
            window.location.reload();
        } else {
            alert(data.message || "Failed to update task status.");
        }
    } catch (err) {
        console.error("Status update error", err);
    }
}

// Check initial due tasks on page load
async function checkActiveAlertsOnLoad() {
    try {
        const res = await fetch('/Todos/GetActiveAlerts');
        if (res.ok) {
            const dueTasks = await res.json();
            if (Array.isArray(dueTasks) && dueTasks.length > 0) {
                dueTasks.forEach(task => {
                    addNotificationToList(task);
                });
            }
        }
    } catch (e) {
        // Silently catch if unauthenticated
    }
}

// Overlap validation helper for modals
let overlapCheckDebounceTimer = null;

function checkModalTimeOverlap() {
    clearTimeout(overlapCheckDebounceTimer);
    overlapCheckDebounceTimer = setTimeout(async () => {
        const startInput = document.getElementById("todoStartTime");
        const endInput = document.getElementById("todoEndTime");
        const todoIdInput = document.getElementById("todoId");
        const userSelect = document.getElementById("todoAssignedUser");
        const alertBox = document.getElementById("modalOverlapAlert");
        const alertMsg = document.getElementById("modalOverlapMsg");
        const submitBtn = document.getElementById("btnSaveTodo");

        if (!startInput || !endInput || !startInput.value || !endInput.value) {
            if (alertBox) alertBox.classList.remove("show");
            return;
        }

        const startTime = startInput.value;
        const endTime = endInput.value;
        const excludeId = todoIdInput ? todoIdInput.value : '';
        const targetUserId = userSelect ? userSelect.value : '';

        // Check if endTime <= startTime
        if (new Date(endTime) <= new Date(startTime)) {
            if (alertBox && alertMsg) {
                alertMsg.innerHTML = "<strong>Invalid Time Range:</strong> Expected End Time must be later than Start Time.";
                alertBox.classList.add("show");
            }
            if (submitBtn) submitBtn.disabled = true;
            return;
        }

        try {
            const url = `/Todos/CheckOverlap?startTime=${encodeURIComponent(startTime)}&expectedEndTime=${encodeURIComponent(endTime)}&excludeId=${excludeId}&targetUserId=${targetUserId}`;
            const res = await fetch(url);
            const data = await res.json();

            if (data.hasOverlap) {
                if (alertBox && alertMsg) {
                    alertMsg.innerHTML = `<strong>Time Conflict Detected:</strong> ${data.message}`;
                    alertBox.classList.add("show");
                }
                if (submitBtn) submitBtn.disabled = true;
            } else {
                if (alertBox) alertBox.classList.remove("show");
                if (submitBtn) submitBtn.disabled = false;
            }
        } catch (err) {
            console.error("Overlap check error", err);
        }
    }, 350);
}

// Save Todo (Create / Edit) via Modal AJAX
async function handleTodoFormSubmit(e) {
    e.preventDefault();
    const form = e.target;
    const submitBtn = document.getElementById("btnSaveTodo");
    const alertBox = document.getElementById("modalOverlapAlert");
    const alertMsg = document.getElementById("modalOverlapMsg");

    const todoId = parseInt(document.getElementById("todoId")?.value || "0");
    const isEdit = todoId > 0;

    const payload = {
        id: todoId,
        title: document.getElementById("todoTitle").value.trim(),
        description: document.getElementById("todoDescription").value.trim(),
        categoryId: parseInt(document.getElementById("todoCategory").value),
        startTime: document.getElementById("todoStartTime").value,
        expectedEndTime: document.getElementById("todoEndTime").value,
        priority: document.getElementById("todoPriority").value,
        status: document.getElementById("todoStatus").value,
        assignedUserId: document.getElementById("todoAssignedUser") ? parseInt(document.getElementById("todoAssignedUser").value) : null
    };

    if (new Date(payload.expectedEndTime) <= new Date(payload.startTime)) {
        if (alertBox && alertMsg) {
            alertMsg.innerHTML = "<strong>Invalid Times:</strong> Expected End Time must be later than Start Time.";
            alertBox.classList.add("show");
        }
        return;
    }

    if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> Saving...';
    }

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const endpoint = isEdit ? '/Todos/Edit' : '/Todos/Create';

    try {
        const response = await fetch(endpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(payload)
        });

        const result = await response.json();

        if (result.success) {
            // Close modal and refresh
            const modalEl = document.getElementById('todoModal');
            if (modalEl) {
                const modal = bootstrap.Modal.getInstance(modalEl);
                if (modal) modal.hide();
            }
            window.location.reload();
        } else {
            // Display conflict error inside the modal!
            if (alertBox && alertMsg) {
                alertMsg.innerHTML = `<strong>Conflict / Error:</strong> ${result.message}`;
                alertBox.classList.add("show");
            }
        }
    } catch (err) {
        console.error("Error saving task", err);
        if (alertBox && alertMsg) {
            alertMsg.innerHTML = "An unexpected error occurred while saving the task. Please try again.";
            alertBox.classList.add("show");
        }
    } finally {
        if (submitBtn) {
            submitBtn.disabled = false;
            submitBtn.innerHTML = '<i class="bi bi-check-lg me-1"></i> Save Task';
        }
    }
}

// Open Modal for New Todo
function openCreateTodoModal() {
    const form = document.getElementById("todoModalForm");
    if (form) form.reset();

    document.getElementById("todoModalTitle").textContent = "Create New Todo";
    document.getElementById("todoId").value = "0";

    const alertBox = document.getElementById("modalOverlapAlert");
    if (alertBox) alertBox.classList.remove("show");

    // Set default start time to now (rounded up to next 15 mins) and end time + 1 hr
    const now = new Date();
    now.setMinutes(Math.ceil(now.getMinutes() / 15) * 15, 0, 0);
    const end = new Date(now.getTime() + 60 * 60 * 1000);

    const formatToLocalInput = (d) => {
        const pad = (n) => String(n).padStart(2, '0');
        return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    };

    document.getElementById("todoStartTime").value = formatToLocalInput(now);
    document.getElementById("todoEndTime").value = formatToLocalInput(end);

    const submitBtn = document.getElementById("btnSaveTodo");
    if (submitBtn) submitBtn.disabled = false;

    const modal = new bootstrap.Modal(document.getElementById('todoModal'));
    modal.show();

    // Check overlap with defaults
    checkModalTimeOverlap();
}

// Open Modal for Edit Todo
async function openEditTodoModal(todoId) {
    try {
        const res = await fetch(`/Todos/GetTodo?id=${todoId}`);
        const data = await res.json();

        if (data.success) {
            document.getElementById("todoModalTitle").textContent = "Edit Todo";
            document.getElementById("todoId").value = data.id;
            document.getElementById("todoTitle").value = data.title;
            document.getElementById("todoDescription").value = data.description || '';
            document.getElementById("todoCategory").value = data.categoryId;
            document.getElementById("todoStartTime").value = data.startTime;
            document.getElementById("todoEndTime").value = data.expectedEndTime;
            document.getElementById("todoStatus").value = data.status;
            document.getElementById("todoPriority").value = data.priority;

            const userSelect = document.getElementById("todoAssignedUser");
            if (userSelect && data.assignedUserId) {
                userSelect.value = data.assignedUserId;
            }

            const alertBox = document.getElementById("modalOverlapAlert");
            if (alertBox) alertBox.classList.remove("show");

            const submitBtn = document.getElementById("btnSaveTodo");
            if (submitBtn) submitBtn.disabled = false;

            const modal = new bootstrap.Modal(document.getElementById('todoModal'));
            modal.show();
        }
    } catch (e) {
        console.error("Failed to load task for edit", e);
    }
}

// Delete Todo
async function deleteTodo(todoId, title) {
    if (!confirm(`Are you sure you want to delete task "${title}"?`)) {
        return;
    }

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    try {
        const res = await fetch(`/Todos/Delete?id=${todoId}`, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            }
        });
        const result = await res.json();
        if (result.success) {
            window.location.reload();
        } else {
            alert(result.message || "Failed to delete task.");
        }
    } catch (e) {
        console.error("Error deleting task", e);
    }
}

// Setup listeners on DOM Ready
document.addEventListener("DOMContentLoaded", function () {
    initSignalR();
    checkActiveAlertsOnLoad();

    const startInput = document.getElementById("todoStartTime");
    const endInput = document.getElementById("todoEndTime");
    const userSelect = document.getElementById("todoAssignedUser");

    if (startInput) startInput.addEventListener("change", checkModalTimeOverlap);
    if (endInput) endInput.addEventListener("change", checkModalTimeOverlap);
    if (userSelect) userSelect.addEventListener("change", checkModalTimeOverlap);

    const todoForm = document.getElementById("todoModalForm");
    if (todoForm) {
        todoForm.addEventListener("submit", handleTodoFormSubmit);
    }
});
