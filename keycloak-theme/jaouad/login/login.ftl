<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=true displayInfo=realm.password && realm.registrationAllowed && !registrationDisabled??; section>
    <#if section = "header">
        ${msg("loginAccountTitle")}
    <#elseif section = "form">
        <div class="auth-form-section">
            <h1 class="auth-title">Log in to NextStep</h1>

            <#if message?has_content && (message.type != 'warning' || !isAppInitiatedAction??)>
                <div class="alert alert-${message.type}">
                    <span>${kcSanitize(message.summary)?no_esc}</span>
                </div>
            </#if>

            <form id="kc-form-login" onsubmit="login.disabled = true; return true;" action="${url.loginAction}" method="post">
                <div class="auth-field">
                    <label for="username">Email Address</label>
                    <input
                        tabindex="1"
                        id="username"
                        name="username"
                        value="${(login.username!'')}"
                        type="text"
                        autofocus
                        autocomplete="username"
                        placeholder="Enter your email address"
                        class="<#if messagesPerField.existsError('username','password')>input-error</#if>"
                    />
                </div>

                <div class="auth-field">
                    <div class="auth-field-header">
                        <label for="password">Password</label>
                        <#if realm.resetPasswordAllowed>
                            <a tabindex="5" href="${url.loginResetCredentialsUrl}" class="auth-field-link">Forgot password?</a>
                        </#if>
                    </div>
                    <div class="auth-input-wrapper">
                        <input
                            tabindex="2"
                            id="password"
                            name="password"
                            type="password"
                            autocomplete="current-password"
                            placeholder="Enter your password"
                            class="<#if messagesPerField.existsError('username','password')>input-error</#if>"
                        />
                        <button type="button" class="auth-password-toggle" onclick="togglePasswordVisibility('password', 'pwd-icon-login')" tabindex="-1">
                            <svg id="pwd-icon-login" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
                                <circle cx="12" cy="12" r="3"/>
                            </svg>
                        </button>
                    </div>
                </div>

                <button tabindex="4" class="auth-btn-primary" name="login" id="kc-login" type="submit">Sign in</button>
            </form>

            <#if realm.password>
                <#if social?? && social.providers??>
                    <div class="auth-divider">
                        <span>Or Sign in with</span>
                    </div>
                    <div class="auth-social-providers">
                        <#list social.providers as p>
                            <#if p.alias == "google">
                                <a href="${p.loginUrl}" class="auth-btn-google" id="social-google">
                                    <svg width="18" height="18" viewBox="0 0 48 48" xmlns="http://www.w3.org/2000/svg">
                                        <path fill="#000" d="M43.611,20.083H42V20H24v8h11.303c-1.649,4.657-6.08,8-11.303,8c-6.627,0-12-5.373-12-12c0-6.627,5.373-12,12-12c3.059,0,5.842,1.154,7.961,3.039l5.657-5.657C34.046,6.053,29.268,4,24,4C12.955,4,4,12.955,4,24c0,11.045,8.955,20,20,20c11.045,0,20-8.955,20-20C44,22.659,43.862,21.35,43.611,20.083z"/>
                                    </svg>
                                    Continue with Google
                                </a>
                            </#if>
                        </#list>
                    </div>
                </#if>
            </#if>
        </div>

        <script>
            function togglePasswordVisibility(inputId, iconId) {
                const input = document.getElementById(inputId);
                const icon = document.getElementById(iconId);
                if (!input || !icon) return;
                if (input.type === 'password') {
                    input.type = 'text';
                    icon.innerHTML = '<path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/>';
                } else {
                    input.type = 'password';
                    icon.innerHTML = '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>';
                }
            }
        </script>

    <#elseif section = "info" >
        <#if realm.password && realm.registrationAllowed && !registrationDisabled??>
            <div class="auth-switch">
                <span>Don't have an account? <a href="${url.registrationUrl}" class="auth-link-teal">Sign up</a></span>
            </div>
        </#if>
    </#if>
</@layout.registrationLayout>
