<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=!messagesPerField.existsError('password','password-confirm'); section>
    <#if section = "header">
        ${msg("updatePasswordTitle")}
    <#elseif section = "form">
        <form id="kc-passwd-update-form" class="form" action="${url.loginAction}" method="post">
            
            <div class="form-group">
                <label for="password-new">${msg("passwordNew")}</label>
                <div class="input-wrapper">
                    <i data-feather="lock" class="input-icon"></i>
                    <input type="password" id="password-new" name="password-new" class="" autofocus autocomplete="new-password" placeholder="••••••••" />
                    <button type="button" class="password-toggle" onclick="togglePassword('password-new', 'password-new-icon')">
                        <i data-feather="eye" id="password-new-icon"></i>
                    </button>
                </div>
                <#if messagesPerField.existsError('password')>
                    <span id="input-error-password" class="error-text" aria-live="polite">
                        <i data-feather="alert-circle"></i>
                        ${kcSanitize(messagesPerField.get('password'))?no_esc}
                    </span>
                </#if>
            </div>

            <div class="form-group">
                <label for="password-confirm">${msg("passwordConfirm")}</label>
                <div class="input-wrapper">
                    <i data-feather="lock" class="input-icon"></i>
                    <input type="password" id="password-confirm" name="password-confirm" class="" autocomplete="new-password" placeholder="••••••••" />
                    <button type="button" class="password-toggle" onclick="togglePassword('password-confirm', 'password-confirm-icon')">
                        <i data-feather="eye" id="password-confirm-icon"></i>
                    </button>
                </div>
                <#if messagesPerField.existsError('password-confirm')>
                    <span id="input-error-password-confirm" class="error-text" aria-live="polite">
                        <i data-feather="alert-circle"></i>
                        ${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}
                    </span>
                </#if>
            </div>

            <div class="form-options">
                <div class="remember-me">
                    <#if logoutSessions??>
                        <label><input type="checkbox" id="logout-sessions" name="logout-sessions" value="on" checked> ${msg("logoutOtherSessions")}</label>
                    <#else>
                        <label><input type="checkbox" id="logout-sessions" name="logout-sessions" value="on" checked> ${msg("logoutOtherSessions")}</label>
                    </#if>
                </div>
            </div>

            <div class="form-group" style="margin-top: 1.5rem;">
                <button class="btn-primary" type="submit">Update Password & Finish &rarr;</button>
            </div>
        </form>

        <script>
            function togglePassword(inputId, iconId) {
                const passwordInput = document.getElementById(inputId);
                const passwordIcon = document.getElementById(iconId);
                if (passwordInput.type === 'password') {
                    passwordInput.type = 'text';
                    passwordIcon.outerHTML = `<i data-feather=\"eye-off\" id=\"${iconId}\"></i>`;
                        if(typeof feather !== 'undefined') feather.replace();
                } else {
                    passwordInput.type = 'password';
                    passwordIcon.outerHTML = `<i data-feather=\"eye\" id=\"${iconId}\"></i>`;
                        if(typeof feather !== 'undefined') feather.replace();
                }
            }
        </script>
    </#if>
</@layout.registrationLayout>
