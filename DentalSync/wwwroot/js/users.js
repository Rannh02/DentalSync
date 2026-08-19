(function () {
    var modal = document.getElementById('createUserModal');
    var openButton = document.getElementById('openCreateUser');
    if (!modal || !openButton) return;

    var passwordInput = document.getElementById('createPassword');
    var strengthBar = document.getElementById('passwordStrengthBar');
    var strengthLabel = document.getElementById('passwordStrengthLabel');

    function updatePasswordStrength() {
        var password = passwordInput.value;
        var score = 0;
        if (password.length >= 12) score++;
        if (/[a-z]/.test(password)) score++;
        if (/[A-Z]/.test(password)) score++;
        if (/[0-9]/.test(password)) score++;
        if (/[^a-zA-Z0-9]/.test(password)) score++;

        var levels = [
            { label: 'Enter a password', className: '', width: '0%' },
            { label: 'Very weak', className: 'is-very-weak', width: '20%' },
            { label: 'Weak', className: 'is-weak', width: '40%' },
            { label: 'Fair', className: 'is-fair', width: '60%' },
            { label: 'Strong', className: 'is-strong', width: '80%' },
            { label: 'Very strong', className: 'is-very-strong', width: '100%' }
        ];
        var level = levels[password.length === 0 ? 0 : score];
        strengthBar.className = level.className;
        strengthBar.style.width = level.width;
        strengthLabel.textContent = level.label;
    }

    function closeModal() {
        modal.classList.remove('is-open');
        modal.setAttribute('aria-hidden', 'true');
    }

    passwordInput.addEventListener('input', updatePasswordStrength);
    openButton.addEventListener('click', function () {
        modal.classList.add('is-open');
        modal.setAttribute('aria-hidden', 'false');
        document.getElementById('createFullName').focus();
    });
    modal.querySelectorAll('[data-close-user-modal]').forEach(function (button) {
        button.addEventListener('click', closeModal);
    });
    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') closeModal();
    });
})();
