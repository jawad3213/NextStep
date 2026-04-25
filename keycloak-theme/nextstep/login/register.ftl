<#import "template.ftl" as layout>
<@layout.registrationLayout displayMessage=true displayInfo=true; section>
    <#if section = "header">
        ${msg("registerTitle")}
    <#elseif section = "form">
        <div class="auth-form-section">
            <h1 class="auth-title">Create your account</h1>

            <!-- Global Messages (Errors/Success/Info) -->
            <#if message?has_content && (message.type != 'warning' || !isAppInitiatedAction??)>
                <div class="alert alert-${message.type}">
                    <#if message.type = 'success'><i data-feather="check-circle" style="width: 14px; height: 14px; margin-right: 8px;"></i></#if>
                    <#if message.type = 'error'><i data-feather="alert-circle" style="width: 14px; height: 14px; margin-right: 8px;"></i></#if>
                    <#if message.type = 'info'><i data-feather="info" style="width: 14px; height: 14px; margin-right: 8px;"></i></#if>
                    <span>${kcSanitize(message.summary)?no_esc}</span>
                </div>
            </#if>

            <form id="kc-register-form" action="${url.registrationAction}" method="post">
                <div class="auth-field">
                    <label>Legal Names</label>
                    <input type="text" id="firstName" name="firstName" value="${(register.formData.firstName!'')}" placeholder="First Name" style="margin-bottom: 12px;" class="<#if messagesPerField.existsError('firstName')>input-error</#if>" />
                    <input type="text" id="lastName" name="lastName" value="${(register.formData.lastName!'')}" placeholder="Last Name" class="<#if messagesPerField.existsError('lastName')>input-error</#if>" />
                </div>

                <div class="auth-field">
                    <label for="email">Email Address</label>
                    <input type="text" id="email" name="email" value="${(register.formData.email!'')}" autocomplete="email" placeholder="Enter your email address" class="<#if messagesPerField.existsError('email')>input-error</#if>" />
                    <#if messagesPerField.existsError('email')>
                        <span class="auth-field-error">${kcSanitize(messagesPerField.get('email'))?no_esc}</span>
                    </#if>
                </div>


                <#if passwordRequired??>
                    <div class="auth-field">
                        <label for="password">Password</label>
                        <input type="password" id="password" name="password" autocomplete="new-password" placeholder="Enter your password" style="margin-bottom: 12px;" class="<#if messagesPerField.existsError('password')>input-error</#if>" />
                        <#if messagesPerField.existsError('password')>
                            <span class="auth-field-error">${kcSanitize(messagesPerField.get('password'))?no_esc}</span>
                        </#if>
                        
                        <input type="password" id="password-confirm" name="password-confirm" autocomplete="new-password" placeholder="Confirm your password" class="<#if messagesPerField.existsError('password-confirm')>input-error</#if>" />
                        <#if messagesPerField.existsError('password-confirm')>
                            <span class="auth-field-error">${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}</span>
                        </#if>
                    </div>
                </#if>

           

                <button class="auth-btn-primary" type="submit">Create account</button>
            </form>

            <#if realm.password>
                <!-- Social Providers at Bottom -->
                <#if social?? && social.providers??>
                    <div class="auth-divider">
                        <span>Or Sign up with</span>
                    </div>
                    <div class="auth-social-providers">
                        <#list social.providers as p>
                            <#if p.alias == "google">
                                <a href="${p.loginUrl}" class="auth-btn-google" id="social-google">
                                    <svg width="18" height="18" viewBox="0 0 48 48" xmlns="http://www.w3.org/2000/svg"><path fill="#000" d="M43.611,20.083H42V20H24v8h11.303c-1.649,4.657-6.08,8-11.303,8c-6.627,0-12-5.373-12-12c0-6.627,5.373-12,12-12c3.059,0,5.842,1.154,7.961,3.039l5.657-5.657C34.046,6.053,29.268,4,24,4C12.955,4,4,12.955,4,24c0,11.045,8.955,20,20,20c11.045,0,20-8.955,20-20C44,22.659,43.862,21.35,43.611,20.083z"/></svg>
                                    Continue with Google
                                </a>
                            </#if>
                        </#list>
                    </div>
                </#if>
            </#if>
        </div>

    <#elseif section = "info" >
        <div class="auth-switch">
            <span>Already have an account? <a href="${url.loginUrl}" class="auth-link-teal">Sign in</a></span>
        </div>
    </#if>
</@layout.registrationLayout>
