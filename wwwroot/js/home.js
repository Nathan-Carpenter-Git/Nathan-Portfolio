// ── Data registry: single source of truth for badge blurbs and cert details ──
const CERT_DATA = {
    'network-plus': {
        name: 'CompTIA Network+',
        image: '/shared/ComptiaNetwork.png',
        issuer: 'CompTIA',
        issueDate: 'Apr 2026',
        credentialId: '25fc901a796244fdb6b1ca2b65ed5b4b',
        verifyUrl: 'https://cp.certmetrics.com/CompTIA/en/public/verify/credential/25fc901a796244fdb6b1ca2b65ed5b4b'
    },
    'az-900': {
        name: 'Azure Fundamentals AZ-900',
        image: '/shared/AZ-900.png',
        issuer: 'Microsoft',
        issueDate: 'Jan 2026',
        credentialId: '180C60002E067487',
        verifyUrl: 'https://learn.microsoft.com/api/credentials/share/en-us/NathanCarpenter-7803/180C60002E067487?sharingId=64AB9FA729238809'
    }
};

const BADGE_DATA = {
    'network-plus': {
        text: 'Hands-on experience with routing, switching, and network troubleshooting across multi-site environments.',
        certId: 'network-plus'
    },
    'az-900': {
        text: 'Deployed and managed Azure Virtual Networks, Virtual Machines, and App Services in real projects.',
        certId: 'az-900'
    },
    'windows-server': {
        text: 'Administered Windows Server environments including AD DS, DNS, DHCP, and NPS.'
    },
    'entra-id': {
        text: 'Configured identity, SSO, and certificate-based access policies using Microsoft Entra ID.'
    },
    'intune': {
        text: 'Managed device compliance, policies, and app deployment through Microsoft Intune.'
    },
    'virtualization': {
        text: 'Built and maintained virtualized infrastructure for servers and stateless workloads.'
    }
};

document.addEventListener('DOMContentLoaded', () => {
    const popover = document.getElementById('techPopover');
    const popoverText = popover.querySelector('.tech-popover-text');
    const popoverLink = popover.querySelector('.tech-popover-link');

    const modalOverlay = document.getElementById('certModalOverlay');
    const modalClose = document.getElementById('certModalClose');
    const modalImg = document.getElementById('certModalImg');
    const modalName = document.getElementById('certModalName');
    const modalIssuer = document.getElementById('certModalIssuer');
    const modalDate = document.getElementById('certModalDate');
    const modalCredId = document.getElementById('certModalCredId');
    const modalVerify = document.getElementById('certModalVerify');

    let activeBadge = null;

    function closePopover() {
        popover.classList.remove('open');
        popover.hidden = true;
        activeBadge = null;
    }

    function openPopover(badgeEl, data) {
        activeBadge = badgeEl;
        popoverText.textContent = data.text;

        if (data.certId) {
            popoverLink.hidden = false;
            popoverLink.onclick = (e) => {
                e.preventDefault();
                closePopover();
                openCertModal(data.certId);
            };
        } else {
            popoverLink.hidden = true;
            popoverLink.onclick = null;
        }

        popover.hidden = false;
        const rect = badgeEl.getBoundingClientRect();
        const popoverWidth = popover.offsetWidth || 260;
        let left = rect.left;
        left = Math.min(left, window.innerWidth - popoverWidth - 12);
        left = Math.max(left, 12);
        const top = rect.bottom + 10;

        popover.style.left = `${left}px`;
        popover.style.top = `${top}px`;
        popover.querySelector('.tech-popover-arrow').style.left = `${Math.max(12, rect.left - left + rect.width / 2 - 6)}px`;

        requestAnimationFrame(() => popover.classList.add('open'));
    }

    function closeModal() {
        modalOverlay.classList.remove('open');
        modalOverlay.hidden = true;
        document.body.style.overflow = '';
    }

    function openCertModal(certId) {
        const data = CERT_DATA[certId];
        if (!data) return;

        modalImg.src = data.image;
        modalImg.alt = `${data.name} Certificate`;
        modalName.textContent = data.name;
        modalIssuer.textContent = data.issuer;
        modalDate.textContent = data.issueDate;
        modalCredId.textContent = data.credentialId;
        modalVerify.href = data.verifyUrl;

        modalOverlay.hidden = false;
        document.body.style.overflow = 'hidden';
        requestAnimationFrame(() => modalOverlay.classList.add('open'));
        modalClose.focus();
    }

    // ── Badge popover wiring ──
    document.querySelectorAll('[data-badge]').forEach(el => {
        const data = BADGE_DATA[el.dataset.badge];
        if (!data) return;

        const activate = (e) => {
            e.stopPropagation();
            if (activeBadge === el && popover.classList.contains('open')) {
                closePopover();
            } else {
                openPopover(el, data);
            }
        };

        el.addEventListener('click', activate);
        el.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                activate(e);
            }
        });
    });

    document.addEventListener('click', (e) => {
        if (!popover.hidden && !popover.contains(e.target) && e.target !== activeBadge) {
            closePopover();
        }
    });

    window.addEventListener('scroll', () => {
        if (!popover.hidden) closePopover();
    }, { passive: true });

    // ── Cert modal wiring ──
    document.querySelectorAll('[data-cert]').forEach(el => {
        const activate = (e) => {
            e.stopPropagation();
            openCertModal(el.dataset.cert);
        };

        el.addEventListener('click', activate);
        el.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                activate(e);
            }
        });
    });

    modalClose.addEventListener('click', closeModal);
    modalOverlay.addEventListener('click', (e) => {
        if (e.target === modalOverlay) closeModal();
    });

    document.addEventListener('keydown', (e) => {
        if (e.key !== 'Escape') return;
        if (!modalOverlay.hidden) closeModal();
        else if (!popover.hidden) closePopover();
    });
});
