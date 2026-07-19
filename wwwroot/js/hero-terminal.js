document.addEventListener('DOMContentLoaded', () => {
    const body = document.getElementById('terminalBody');
    const output = document.getElementById('terminalOutput');
    const input = document.getElementById('terminalInput');
    if (!body || !output || !input) return;

    const history = [];
    let historyIndex = 0;

    function escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    function scrollToId(id) {
        document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    function scrollToBottom() {
        body.scrollTop = body.scrollHeight;
    }

    function appendLine(html, className) {
        const div = document.createElement('div');
        if (className) div.className = className;
        div.innerHTML = html;
        output.appendChild(div);
    }

    function appendEcho(commandText) {
        const div = document.createElement('div');
        div.style.marginTop = '8px';
        const prompt = document.createElement('span');
        prompt.className = 't-prompt';
        prompt.textContent = '▶';
        const cmd = document.createElement('span');
        cmd.className = 't-cmd';
        cmd.textContent = ' ' + commandText;
        div.appendChild(prompt);
        div.appendChild(cmd);
        output.appendChild(div);
    }

    function cmdLink(name) {
        return `<button type="button" class="t-cmd-link" data-run="${name}">${name}</button>`;
    }

    const COMMANDS = {
        help: {
            desc: 'list available commands',
            run: () => printHelp()
        },
        whoami: {
            desc: 'display current identity',
            run: () => appendLine('nathan-carpenter, IT &amp; Network Professional', 't-out')
        },
        about: {
            desc: 'read a short bio',
            run: () => {
                appendLine('Building secure, scalable infrastructure: from multi-site banking networks to Azure cloud deployments.', 't-out');
                appendLine('→ scrolling to About Me…', 't-out t-dim');
                scrollToId('about');
            }
        },
        skills: {
            desc: 'core technical skills',
            aliases: ['cat skills.txt'],
            run: () => {
                ['Networking', 'Cloud Infrastructure (Azure)', 'SQL Databases', 'System Administration', 'Powershell']
                    .forEach(line => appendLine(line, 't-out'));
            }
        },
        certs: {
            desc: 'view certifications',
            aliases: ['certifications'],
            run: () => {
                ['CompTIA Network+', 'Azure Fundamentals AZ-900', 'AIT Certificate', 'TestOut Network Pro', 'TestOut PC Pro']
                    .forEach(line => appendLine(line, 't-out'));
                appendLine('→ scrolling to Certifications…', 't-out t-dim');
                scrollToId('certifications');
            }
        },
        projects: {
            desc: 'notable projects',
            run: () => {
                appendLine('Wi-Fi Hardening: replaced insecure Wi-Fi with RADIUS + EAP-TLS, HA NPS, isolated BYOD access', 't-out');
                appendLine('Zabbix SNMP System: containerized monitoring stack with SNMP traps and executive SLA reporting', 't-out');
                appendLine('→ for GitHub repos and shipped games, see the <a href="/Projects">Projects page</a>.', 't-out t-dim');
            }
        },
        resume: {
            desc: 'view or download the résumé',
            aliases: ['cat resume'],
            run: () => {
                const resumeUrl = document.querySelector('[data-resume-url]')?.dataset.resumeUrl || '/shared/docs/resume.pdf';
                appendLine('→ scrolling to Résumé… (or <a href="' + resumeUrl + '" download>download the PDF</a>)', 't-out t-dim');
                scrollToId('resume');
            }
        },
        contact: {
            desc: 'get in touch',
            run: () => appendLine('Reach out via the <a href="/Contact">Contact page</a>.', 't-out')
        },
        social: {
            desc: 'social + project links',
            aliases: ['links'],
            run: () => {
                appendLine('<a href="https://github.com/Nathan-Carpenter-Git" target="_blank" rel="noopener">github.com/Nathan-Carpenter-Git</a>', 't-out');
                appendLine('<a href="https://www.linkedin.com/in/nathan-b-carpenter/" target="_blank" rel="noopener">linkedin.com/in/nathan-b-carpenter</a>', 't-out');
                appendLine('<a href="https://dusty-studios.itch.io" target="_blank" rel="noopener">dusty-studios.itch.io</a>', 't-out');
            }
        },
        status: {
            desc: 'current availability',
            run: () => appendLine('Open to opportunities', 't-out')
        },
        clear: {
            desc: 'clear the terminal',
            aliases: ['cls'],
            run: () => { output.innerHTML = ''; }
        },
        sudo: {
            hidden: true,
            run: () => appendLine("Nice try. Permission denied: 'you' is not in the sudoers file. This incident will be reported. (Kidding, try " + cmdLink('contact') + " instead.)", 't-out t-error')
        },
        ls: {
            hidden: true,
            run: () => appendLine('about.md  certifications.json  resume.pdf  projects/  contact.sh, try ' + cmdLink('help') + ' to actually run something', 't-out')
        }
    };

    function printHelp() {
        appendLine('Available commands:', 't-out');
        Object.entries(COMMANDS)
            .filter(([, cmd]) => !cmd.hidden)
            .forEach(([name, cmd]) => {
                appendLine(`${cmdLink(name)}<span class="t-help-desc">: ${cmd.desc}</span>`, 't-out t-help-line');
            });
    }

    function runCommand(raw) {
        const trimmed = raw.trim();
        if (!trimmed) return;

        appendEcho(trimmed);
        history.push(trimmed);
        historyIndex = history.length;

        const key = trimmed.toLowerCase();
        const match = Object.entries(COMMANDS).find(([name, cmd]) =>
            name === key || (cmd.aliases || []).includes(key)
        );

        if (!match) {
            appendLine(`command not found: ${escapeHtml(trimmed)}. Type ${cmdLink('help')} to see available commands.`, 't-out t-error');
        } else {
            match[1].run();
        }

        scrollToBottom();
    }

    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            const value = input.value;
            input.value = '';
            runCommand(value);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            if (historyIndex > 0) {
                historyIndex--;
                input.value = history[historyIndex] || '';
            }
        } else if (e.key === 'ArrowDown') {
            e.preventDefault();
            if (historyIndex < history.length - 1) {
                historyIndex++;
                input.value = history[historyIndex] || '';
            } else {
                historyIndex = history.length;
                input.value = '';
            }
        }
    });

    // Clicking a command link (from `help` output or an error hint) runs it
    output.addEventListener('click', (e) => {
        const link = e.target.closest('.t-cmd-link');
        if (!link) return;
        input.focus();
        runCommand(link.dataset.run);
    });

    // Clicking anywhere else in the terminal focuses the input, like a real one
    body.addEventListener('click', (e) => {
        if (e.target === input || e.target.closest('a, button')) return;
        input.focus();
    });

    printHelp();
    scrollToBottom();
});
