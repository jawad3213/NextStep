<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=true displayInfo=true; section>
    <#if section = "header">
        ${msg("registerTitle")}
    <#elseif section = "form">
        <div class="auth-form-section">
            <h1 class="auth-title">Create an account</h1>

            <#if message?has_content && (message.type != 'warning' || !isAppInitiatedAction??)>
                <div class="alert alert-${message.type}">
                    <span>${kcSanitize(message.summary)?no_esc}</span>
                </div>
            </#if>

            <form id="kc-register-form" action="${url.registrationAction}" method="post">
                <div class="auth-field">
                    <label for="firstName">First Name</label>
                    <input
                        type="text"
                        id="firstName"
                        name="firstName"
                        value="${(register.formData.firstName!'')}"
                        autocomplete="given-name"
                        placeholder="Enter your first name"
                        class="<#if messagesPerField.existsError('firstName')>input-error</#if>"
                    />
                </div>

                <div class="auth-field">
                    <label for="lastName">Last Name</label>
                    <input
                        type="text"
                        id="lastName"
                        name="lastName"
                        value="${(register.formData.lastName!'')}"
                        autocomplete="family-name"
                        placeholder="Enter your last name"
                        class="<#if messagesPerField.existsError('lastName')>input-error</#if>"
                    />
                    <#if messagesPerField.existsError('firstName') || messagesPerField.existsError('lastName')>
                        <span class="auth-field-error">${kcSanitize(messagesPerField.get('firstName'))?no_esc} ${kcSanitize(messagesPerField.get('lastName'))?no_esc}</span>
                    </#if>
                </div>

                <div class="auth-field">
                    <label for="email">Email Address</label>
                    <input
                        type="text"
                        id="email"
                        name="email"
                        value="${(register.formData.email!'')}"
                        autocomplete="email"
                        placeholder="Enter your email address"
                        class="<#if messagesPerField.existsError('email')>input-error</#if>"
                    />
                    <#if messagesPerField.existsError('email')>
                        <span class="auth-field-error">${kcSanitize(messagesPerField.get('email'))?no_esc}</span>
                    </#if>
                </div>

                <#if passwordRequired??>
                    <div class="auth-field">
                        <div class="auth-field-header">
                            <label for="password">Password</label>
                        </div>
                        <div class="auth-input-wrapper">
                            <input
                                type="password"
                                id="password"
                                name="password"
                                autocomplete="new-password"
                                placeholder="Enter your password"
                                class="<#if messagesPerField.existsError('password')>input-error</#if>"
                            />
                            <button type="button" class="auth-password-toggle" onclick="togglePasswordVisibility('password', 'pwd-icon-reg')" tabindex="-1">
                                <svg id="pwd-icon-reg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
                                    <circle cx="12" cy="12" r="3"/>
                                </svg>
                            </button>
                        </div>
                        <#if messagesPerField.existsError('password')>
                            <span class="auth-field-error">${kcSanitize(messagesPerField.get('password'))?no_esc}</span>
                        </#if>
                    </div>

                    <div class="auth-field">
                        <label for="password-confirm">Confirm Password</label>
                        <input
                            type="password"
                            id="password-confirm"
                            name="password-confirm"
                            autocomplete="new-password"
                            placeholder="Confirm your password"
                            class="<#if messagesPerField.existsError('password-confirm')>input-error</#if>"
                        />
                        <#if messagesPerField.existsError('password-confirm')>
                            <span class="auth-field-error">${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}</span>
                        </#if>
                    </div>
                </#if>

                <button class="auth-btn-primary" type="submit" id="kc-register">Sign up</button>
            </form>

            <#if realm.password>
                <#if social?? && social.providers??>
                    <div class="auth-divider">
                        <span>Or Sign up with</span>
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
        <div class="auth-switch">
            <span>Already have an account? <a href="${url.loginUrl}" class="auth-link-teal">Sign in</a></span>
        </div>
    </#if>
</@layout.registrationLayout>
