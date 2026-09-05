(() => {
    'use strict';

    const page = document.getElementById('ygContactPage');

    if (!page || page.dataset.signalHorizonReady === 'true') {
        return;
    }

    page.dataset.signalHorizonReady = 'true';

    const signalStage = document.getElementById(
        'ygContactSignalStage'
    );

    const messageBody = document.getElementById(
        'MessageBody'
    );

    const messageCounter = document.getElementById(
        'ygMessageCounter'
    );

    const contactForm = document.getElementById(
        'contactForm'
    );

    const designOnlyNote = document.getElementById(
        'ygDesignOnlyNote'
    );

    const finePointer = window.matchMedia(
        '(hover:hover) and (pointer:fine)'
    );

    const reducedMotion = window.matchMedia(
        '(prefers-reduced-motion: reduce)'
    );


    /* =============================================================
       1) REVEAL
       ============================================================= */

    const revealTargets = [
        ...page.querySelectorAll('[data-yg-reveal]')
    ];

    if (
        'IntersectionObserver' in window &&
        !reducedMotion.matches
    ) {
        const revealObserver = new IntersectionObserver(
            (entries, observer) => {
                entries.forEach((entry) => {
                    if (!entry.isIntersecting) {
                        return;
                    }

                    entry.target.classList.add(
                        'is-revealed'
                    );

                    observer.unobserve(
                        entry.target
                    );
                });
            },
            {
                threshold: 0.12,
                rootMargin: '0px 0px -4% 0px'
            }
        );

        revealTargets.forEach((target) => {
            revealObserver.observe(target);
        });
    } else {
        revealTargets.forEach((target) => {
            target.classList.add(
                'is-revealed'
            );
        });
    }


    /* =============================================================
       2) POINTER LIGHT
       Visual only.
       ============================================================= */

    const setPointer = (clientX, clientY) => {
        const rect = page.getBoundingClientRect();

        if (!rect.width || !rect.height) {
            return;
        }

        const x = Math.max(
            0,
            Math.min(
                100,
                (
                    (clientX - rect.left) /
                    rect.width
                ) * 100
            )
        );

        const y = Math.max(
            0,
            Math.min(
                100,
                (
                    (clientY - rect.top) /
                    rect.height
                ) * 100
            )
        );

        page.style.setProperty(
            '--yg-page-pointer-x',
            `${x}%`
        );

        page.style.setProperty(
            '--yg-page-pointer-y',
            `${y}%`
        );
    };


    if (
        finePointer.matches &&
        !reducedMotion.matches
    ) {
        page.addEventListener(
            'pointermove',
            (event) => {
                setPointer(
                    event.clientX,
                    event.clientY
                );
            },
            {
                passive: true
            }
        );

        page.addEventListener(
            'pointerleave',
            () => {
                page.style.setProperty(
                    '--yg-page-pointer-x',
                    '72%'
                );

                page.style.setProperty(
                    '--yg-page-pointer-y',
                    '28%'
                );
            },
            {
                passive: true
            }
        );
    }


    /* =============================================================
       3) SIGNAL ORB PARALLAX
       Desktop fine-pointer only.
       ============================================================= */

    if (
        signalStage &&
        finePointer.matches &&
        !reducedMotion.matches
    ) {
        signalStage.addEventListener(
            'pointermove',
            (event) => {
                const rect =
                    signalStage.getBoundingClientRect();

                if (!rect.width || !rect.height) {
                    return;
                }

                const dx =
                    (
                        event.clientX -
                        rect.left -
                        rect.width / 2
                    ) /
                    rect.width;

                const dy =
                    (
                        event.clientY -
                        rect.top -
                        rect.height / 2
                    ) /
                    rect.height;

                const orb =
                    signalStage.querySelector(
                        '.yg-contact-signal-orb'
                    );

                if (!orb) {
                    return;
                }

                orb.style.transform =
                    `rotateY(${dx * 7}deg) ` +
                    `rotateX(${dy * -7}deg) ` +
                    `translate3d(${dx * 8}px, ` +
                    `${dy * 8}px, 0)`;
            },
            {
                passive: true
            }
        );

        signalStage.addEventListener(
            'pointerleave',
            () => {
                const orb =
                    signalStage.querySelector(
                        '.yg-contact-signal-orb'
                    );

                if (!orb) {
                    return;
                }

                orb.style.transform =
                    'rotateY(0deg) rotateX(0deg) ' +
                    'translate3d(0,0,0)';
            },
            {
                passive: true
            }
        );
    }


    /* =============================================================
       4) MESSAGE COUNTER
       ============================================================= */

    const updateCounter = () => {
        if (
            !messageBody ||
            !messageCounter
        ) {
            return;
        }

        const count =
            messageBody.value.length;

        messageCounter.textContent =
            `${count} حرف`;
    };


    if (
        messageBody &&
        messageCounter
    ) {
        updateCounter();

        messageBody.addEventListener(
            'input',
            updateCounter,
            {
                passive: true
            }
        );
    }


    /* =============================================================
       5) DESIGN-ONLY FORM GUARD
       Backend programming intentionally postponed.
       No fake success message and no request is sent.
       ============================================================= */

    if (contactForm) {
        contactForm.addEventListener(
            'submit',
            (event) => {
                event.preventDefault();

                if (
                    !contactForm.checkValidity()
                ) {
                    contactForm.reportValidity();
                    return;
                }

                if (designOnlyNote) {
                    designOnlyNote.classList.add(
                        'is-visible'
                    );

                    window.clearTimeout(
                        Number(
                            designOnlyNote.dataset.hideTimer
                        ) || 0
                    );

                    const timer =
                        window.setTimeout(
                            () => {
                                designOnlyNote.classList.remove(
                                    'is-visible'
                                );
                            },
                            4200
                        );

                    designOnlyNote.dataset.hideTimer =
                        String(timer);
                }
            }
        );
    }
})();
