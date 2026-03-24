// =============================================
// AI Resume Assistant - Client-side JavaScript
// =============================================

var _hubConnection = null;
var _hubConnected = false;

// Initialise SignalR connection
function initSignalR() {
    if (typeof signalR === 'undefined') return;

    _hubConnection = new signalR.HubConnectionBuilder()
        .withUrl('/chathub')
        .withAutomaticReconnect()
        .build();

    _hubConnection.start()
        .then(function () { _hubConnected = true; console.log('SignalR connected'); })
        .catch(function (err) { console.warn('SignalR connection failed, falling back to AJAX', err); });

    _hubConnection.onreconnected(function () { _hubConnected = true; });
    _hubConnection.onclose(function () { _hubConnected = false; });
}

// Render server-side Markdown messages on page load
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.ai-response-raw').forEach(function (el) {
        var raw = el.getAttribute('data-raw');
        if (raw) {
            el.innerHTML = renderMarkdown(raw);
        }
    });

    initSignalR();
});

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

// Send a question to the AI (streaming via SignalR, AJAX fallback)
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

    if (_hubConnected) {
        askAIStreaming(question, promptMode, loading, btn, questionInput);
    } else {
        askAIAjax(question, promptMode, loading, btn, questionInput);
    }
}

// Stream AI response via SignalR (typing effect)
function askAIStreaming(question, promptMode, loading, btn, questionInput) {
    var contentEl = createStreamingBubble();
    var fullText = '';

    loading.classList.add('d-none');

    _hubConnection.stream('StreamResponse', question, promptMode)
        .subscribe({
            next: function (chunk) {
                if (chunk.startsWith('[ERROR]')) {
                    appendMessage('error', chunk.substring(7));
                    return;
                }
                fullText += chunk;
                contentEl.innerHTML = renderMarkdown(fullText);
                var chatMessages = document.getElementById('chatMessages');
                chatMessages.scrollTop = chatMessages.scrollHeight;
            },
            error: function (err) {
                console.error('Stream error', err);
                if (!fullText) {
                    appendMessage('error', 'Streaming failed. Please try again.');
                }
                contentEl.classList.remove('streaming-cursor');
                btn.disabled = false;
                questionInput.focus();
            },
            complete: function () {
                if (fullText) {
                    contentEl.innerHTML = renderMarkdown(fullText);
                    document.getElementById('btnDownload').disabled = false;
                }
                contentEl.classList.remove('streaming-cursor');
                btn.disabled = false;
                questionInput.focus();
            }
        });
}

// Fallback: send question via AJAX
function askAIAjax(question, promptMode, loading, btn, questionInput) {
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
        error: function (xhr) {
            if (xhr.status === 429) {
                showRateToast('Too many requests. Please slow down.');
            } else {
                appendMessage('error', 'An error occurred. Please try again.');
            }
        },
        complete: function () {
            loading.classList.add('d-none');
            btn.disabled = false;
            questionInput.focus();
        }
    });
}

// Create an empty assistant bubble for streaming content into
function createStreamingBubble() {
    var chatMessages = document.getElementById('chatMessages');

    var welcome = document.getElementById('welcomeMessage');
    if (welcome) welcome.remove();

    var wrapper = document.createElement('div');
    wrapper.className = 'd-flex mb-3 justify-content-start';

    var bubble = document.createElement('div');
    bubble.style.maxWidth = '70%';
    bubble.className = 'bg-white rounded-4 p-3 shadow-sm';

    var header = document.createElement('div');
    header.className = 'small fw-bold text-primary mb-1';
    header.textContent = '\uD83E\uDD16 AI Assistant';

    var contentEl = document.createElement('div');
    contentEl.className = 'ai-response';
    contentEl.innerHTML = '<span class="text-muted">Thinking...</span>';

    bubble.appendChild(header);
    bubble.appendChild(contentEl);
    wrapper.appendChild(bubble);
    chatMessages.appendChild(wrapper);
    chatMessages.scrollTop = chatMessages.scrollHeight;

    // Add streaming cursor class
    contentEl.classList.add('streaming-cursor');
    return contentEl;
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
        bubble.className = 'bg-white rounded-4 p-3 shadow-sm position-relative';
        bubble.innerHTML = '<div class="small fw-bold text-primary mb-1">🤖 AI Assistant</div>' +
            '<div class="ai-response">' + renderMarkdown(content) + '</div>' +
            '<button class="btn btn-sm btn-outline-secondary position-absolute top-0 end-0 m-2 copy-btn" onclick="copyMessage(this)" title="Copy to clipboard">📋</button>';
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

// Score resume with ATS analysis
function scoreResume() {
    var btn = document.getElementById('btnAtsScore');
    var spinner = document.getElementById('atsSpinner');
    var resultDiv = document.getElementById('atsResult');
    var jobDesc = document.getElementById('jobDescription').value.trim();

    btn.disabled = true;
    spinner.classList.remove('d-none');
    resultDiv.innerHTML = '';

    $.ajax({
        url: '/Resume/ScoreResume',
        type: 'POST',
        contentType: 'application/json',
        headers: { 'X-CSRF-TOKEN': getAntiForgeryToken() },
        data: JSON.stringify({ jobDescription: jobDesc || null }),
        success: function (response) {
            if (response.success) {
                var color = response.overallScore >= 80 ? '#28a745' :
                            response.overallScore >= 60 ? '#ffc107' : '#dc3545';
                var html = '<div class="fw-bold mb-1" style="color:' + color + ';">Score: ' + response.overallScore + '/100</div>';
                html += renderAtsBar('Format', response.formatScore);
                html += renderAtsBar('Keywords', response.keywordScore);
                html += renderAtsBar('Impact', response.impactScore);
                if (response.missingSkills && response.missingSkills.length > 0) {
                    html += '<div class="mt-2 text-muted"><strong>Missing:</strong> ' + escapeHtml(response.missingSkills.join(', ')) + '</div>';
                }
                if (response.suggestions && response.suggestions.length > 0) {
                    html += '<div class="mt-1 text-muted"><strong>Tips:</strong><ul class="mb-0 ps-3">';
                    response.suggestions.forEach(function (s) { html += '<li>' + escapeHtml(s) + '</li>'; });
                    html += '</ul></div>';
                }
                resultDiv.innerHTML = html;
            } else {
                resultDiv.innerHTML = '<div class="text-danger">' + escapeHtml(response.error || 'Scoring failed.') + '</div>';
            }
        },
        error: function (xhr) {
            if (xhr.status === 429) {
                showRateToast('Too many scoring requests. Please wait a moment.');
            } else {
                resultDiv.innerHTML = '<div class="text-danger">An error occurred.</div>';
            }
        },
        complete: function () {
            btn.disabled = false;
            spinner.classList.add('d-none');
        }
    });
}

// Render a mini ATS score bar
function renderAtsBar(label, score) {
    var color = score >= 80 ? '#28a745' : score >= 60 ? '#ffc107' : '#dc3545';
    return '<div class="d-flex align-items-center mb-1">' +
        '<span style="width:70px;">' + label + '</span>' +
        '<div class="ats-bar flex-grow-1 me-2"><div class="ats-bar-fill" style="width:' + score + '%;background:' + color + ';"></div></div>' +
        '<span class="fw-bold" style="width:30px;color:' + color + ';">' + score + '</span>' +
        '</div>';
}

// Show a rate-limit toast notification
function showRateToast(message) {
    var toastEl = document.getElementById('rateToast');
    if (!toastEl) return;
    document.getElementById('rateToastBody').textContent = message;
    var toast = new bootstrap.Toast(toastEl, { delay: 4000 });
    toast.show();
}

// Copy an AI message to clipboard
function copyMessage(btn) {
    var bubble = btn.closest('.position-relative');
    var responseEl = bubble.querySelector('.ai-response');
    if (!responseEl) return;

    var text = responseEl.innerText;
    navigator.clipboard.writeText(text).then(function () {
        var original = btn.textContent;
        btn.textContent = '✅';
        setTimeout(function () { btn.textContent = original; }, 1500);
    });
}

// Export entire chat history as Markdown file
function exportChatMarkdown() {
    var messages = document.querySelectorAll('#chatMessages > .d-flex');
    var md = '# AI Resume Assistant — Chat Export\n\n';

    messages.forEach(function (wrapper) {
        var bubble = wrapper.querySelector('div[style], .bg-white, .bg-danger-subtle');
        if (!bubble) return;

        if (wrapper.classList.contains('justify-content-end')) {
            var userText = bubble.querySelector('div:last-child');
            if (userText) md += '## You\n\n' + userText.textContent.trim() + '\n\n---\n\n';
        } else {
            var aiText = bubble.querySelector('.ai-response');
            if (aiText) md += '## AI Assistant\n\n' + aiText.innerText.trim() + '\n\n---\n\n';
        }
    });

    var blob = new Blob([md], { type: 'text/markdown' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = 'chat-export.md';
    a.click();
    URL.revokeObjectURL(url);
}

// Handle keyboard shortcuts
function handleKeyDown(event) {
    if (event.ctrlKey && event.key === 'Enter') {
        event.preventDefault();
        askAI();
    }
    if (event.key === 'Escape') {
        document.getElementById('userQuestion').value = '';
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

// Render Markdown to styled HTML using marked.js
function renderMarkdown(text) {
    if (typeof marked !== 'undefined') {
        marked.setOptions({ breaks: true, gfm: true });
        return marked.parse(text);
    }
    // Fallback: escape and preserve whitespace
    return '<div style="white-space: pre-wrap;">' + escapeHtml(text) + '</div>';
}
