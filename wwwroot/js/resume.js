// =============================================
// AI Resume Assistant - Client-side JavaScript
// =============================================

// Read the anti-forgery token for CSRF protection
function getAntiForgeryToken() {
    var tokenField = document.querySelector('input[name="__RequestVerificationToken"]');
    return tokenField ? tokenField.value : '';
}

// Upload resume PDF via AJAX
function uploadResume() {
    var fileInput = document.getElementById('resumeFile');
    var file = fileInput.files[0];

    if (!file) {
        showUploadStatus('Please select a PDF file.', 'danger');
        return;
    }

    if (!file.name.toLowerCase().endsWith('.pdf')) {
        showUploadStatus('Only PDF files are supported.', 'danger');
        return;
    }

    var formData = new FormData();
    formData.append('file', file);
    formData.append('__RequestVerificationToken', getAntiForgeryToken());

    var btn = document.getElementById('btnUpload');
    var spinner = document.getElementById('uploadSpinner');
    btn.disabled = true;
    spinner.classList.remove('d-none');

    $.ajax({
        url: '/Resume/UploadResume',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (response) {
            if (response.success) {
                showUploadStatus(response.message, 'success');
                document.getElementById('resumeStatus').innerHTML = '✅ Resume loaded';
                // Update mobile status if present
                var mobileStatus = document.getElementById('resumeStatusMobile');
                if (mobileStatus) {
                    mobileStatus.innerHTML = '✅ Resume loaded';
                    mobileStatus.className = 'text-success';
                }
                // Hide welcome message if present
                var welcome = document.getElementById('welcomeMessage');
                if (welcome) welcome.remove();
            } else {
                showUploadStatus(response.error || 'Upload failed.', 'danger');
            }
        },
        error: function () {
            showUploadStatus('An error occurred during upload.', 'danger');
        },
        complete: function () {
            btn.disabled = false;
            spinner.classList.add('d-none');
        }
    });
}

// Send a question to the AI
function askAI() {
    var questionInput = document.getElementById('userQuestion');
    var question = questionInput.value.trim();

    if (!question) {
        return;
    }

    var promptMode = document.getElementById('promptMode').value;

    // Add user message to chat
    appendMessage('user', question);
    questionInput.value = '';

    // Show loading
    var loading = document.getElementById('loadingIndicator');
    loading.classList.remove('d-none');
    var chatMessages = document.getElementById('chatMessages');
    chatMessages.scrollTop = chatMessages.scrollHeight;

    var btn = document.getElementById('btnSend');
    btn.disabled = true;

    $.ajax({
        url: '/Resume/AskAI',
        type: 'POST',
        contentType: 'application/json',
        headers: { 'X-CSRF-TOKEN': getAntiForgeryToken() },
        data: JSON.stringify({ question: question, promptMode: promptMode }),
        success: function (response) {
            if (response.success) {
                appendMessage('assistant', response.message);
                document.getElementById('btnDownload').disabled = false;
            } else {
                appendMessage('error', response.error || 'Failed to get AI response.');
            }
        },
        error: function () {
            appendMessage('error', 'An error occurred. Please try again.');
        },
        complete: function () {
            loading.classList.add('d-none');
            btn.disabled = false;
            questionInput.focus();
        }
    });
}

// Send a quick action
function sendQuickAction(text) {
    // Close the sidebar offcanvas on mobile
    var sidebar = document.getElementById('sidebarMenu');
    var offcanvasInstance = bootstrap.Offcanvas.getInstance(sidebar);
    if (offcanvasInstance) {
        offcanvasInstance.hide();
    }
    document.getElementById('userQuestion').value = text;
    askAI();
}

// Append a message to the chat area
function appendMessage(role, content) {
    var chatMessages = document.getElementById('chatMessages');

    // Remove welcome message if present
    var welcome = document.getElementById('welcomeMessage');
    if (welcome) welcome.remove();

    var wrapper = document.createElement('div');
    wrapper.className = 'd-flex mb-3 ' + (role === 'user' ? 'justify-content-end' : 'justify-content-start');

    var bubble = document.createElement('div');
    bubble.style.maxWidth = '70%';

    if (role === 'user') {
        bubble.className = 'text-white rounded-4 p-3 shadow-sm';
        bubble.style.background = 'linear-gradient(135deg, #0f3460, #1a1a2e)';
        bubble.innerHTML = '<div class="small fw-bold mb-1 opacity-75">You</div>' +
            '<div>' + escapeHtml(content) + '</div>';
    } else if (role === 'assistant') {
        bubble.className = 'bg-white rounded-4 p-3 shadow-sm';
        bubble.innerHTML = '<div class="small fw-bold text-primary mb-1">🤖 AI Assistant</div>' +
            '<div class="ai-response" style="white-space: pre-wrap;">' + escapeHtml(content) + '</div>';
    } else {
        // error
        bubble.className = 'bg-danger-subtle border border-danger rounded-4 p-3';
        bubble.innerHTML = '<div class="small fw-bold text-danger mb-1">⚠️ Error</div>' +
            '<div>' + escapeHtml(content) + '</div>';
    }

    wrapper.appendChild(bubble);
    chatMessages.appendChild(wrapper);

    // Scroll to bottom
    chatMessages.scrollTop = chatMessages.scrollHeight;
}

// Download the last AI response
function downloadResponse() {
    window.location.href = '/Resume/DownloadLastResponse';
}

// Clear the session
function clearSession() {
    if (!confirm('This will clear your uploaded resume and chat history. Continue?')) {
        return;
    }

    $.ajax({
        url: '/Resume/ClearSession',
        type: 'POST',
        headers: { 'X-CSRF-TOKEN': getAntiForgeryToken() },
        success: function (response) {
            if (response.success) {
                location.reload();
            }
        },
        error: function () {
            alert('Failed to clear session.');
        }
    });
}

// Handle Ctrl+Enter to send
function handleKeyDown(event) {
    if (event.ctrlKey && event.key === 'Enter') {
        event.preventDefault();
        askAI();
    }
}

// Show upload status message
function showUploadStatus(message, type) {
    var statusDiv = document.getElementById('uploadStatus');
    statusDiv.innerHTML = '<div class="alert alert-' + type + ' py-1 px-2 mb-0">' + escapeHtml(message) + '</div>';
}

// Escape HTML to prevent XSS
function escapeHtml(text) {
    var div = document.createElement('div');
    div.appendChild(document.createTextNode(text));
    return div.innerHTML;
}
