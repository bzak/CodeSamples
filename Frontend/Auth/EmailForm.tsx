import * as React from "react"
import classnames = require("classnames");
import { connectedForm, validateEmail, bindFormField, UpdateField, FormField } from "../Commons/forms"
import { LaddaButton } from "../Tags/LaddaButton"
import { setEmail, setRedirectUri, shakeForm, startSubmitting, endSubmitting, setRegistrationInfo, setInvitationInfo, setLicenseInfo, setInitialized } from "./Actions"
import { push } from 'react-router-redux'
import { AccountApi, StoredAuth } from "../Api/api"
import { error } from "../Commons/actions"
import { getCookieValue } from "../Commons/browser"
import { t, thtml } from "../i18n/translate"

interface EmailFormProps  {
    location: HistoryModule.Location;
    email: FormField<string>
    isShaking: boolean;
    isSubmitting: boolean;
    isInitializing: boolean;

    tryPrefill: () => void;
    setEmail: UpdateField<string>;
    setRedirectUri: (uri:string) => void 
    submitEmail: (email: string) => void
}

const boundState = (state) => state.auth;
const boundProps = ['email', 'isShaking', 'isSubmitting', 'isInitializing'];
const boundActions = { setEmail, submitEmail, setRedirectUri, tryPrefill }

export default connectedForm(boundState, boundProps, boundActions)(
    class EmailForm extends React.Component<EmailFormProps, {}> {    
        submit(e) {
            e.preventDefault();
            this.props.submitEmail(this.props.email.value);
        }        
        componentWillReceiveProps(nextProps) {      
            if (nextProps.isInitializing) {                
                nextProps.tryPrefill(nextProps.location);
            }
        }        
        render() {
            return (
                <div>
                    <h1 className="login-title">{t("Auth:Enter your email to login or sign-up")}</h1>
                    <form className="form-signin" onSubmit={(e) => this.submit(e)} noValidate>
                        <div className={
                            classnames({
                                "form-group text-left": true,
                                "has-error": this.props.email.hasError,
                                "shake": this.props.isShaking
                            }) }>
                            <label className="control-label">{t("Auth:Your email")}</label>
                            <input type="email" className="form-control input-lg" placeholder={t("Auth:Email address")} autoFocus
                                value={this.props.email.value} name="email"
                                onChange={bindFormField(this.props.setEmail)}/>
                            { this.props.email.hasError &&
                                <span className="help-block">{t("Auth:The email address is not valid")}</span>
                            }
                        </div>
                        <LaddaButton loading={this.props.isSubmitting}
                            className="btn btn-lg btn-primary btn-block"
                            type="submit">{t("Auth:Next")}</LaddaButton>                        
                    </form>                    
                </div>
            )
        }
    }
)

function tryPrefill(location: HistoryModule.Location) {
    return dispatch => {
        let redirect_uri = location.query["redirect_uri"];
        if (redirect_uri)
            dispatch(setRedirectUri(redirect_uri));
        
        dispatch(setInitialized());
        let auth = StoredAuth();

        // has the user logged in before?
        let email = null;
        if (auth && auth.userName) {
            email = auth.userName;            
        }

        // do we have an email cookie
        let emailCookie = getCookieValue("email");        
        if (emailCookie) {
            email = decodeURIComponent(emailCookie);
        }   

        if (email) {
            dispatch(setEmail({ value: email, hasError: false }));
            dispatch(push("/auth/password"));
        }     
    }
}

export function submitEmail(email: string) {
    return dispatch => {
        if (!validateEmail(email)) {
            dispatch(shakeForm());
            dispatch(setEmail({ value: email, hasError: true }));
        } else {
            dispatch(startSubmitting());

            let api = new AccountApi();
            api.accountGetUserStatus({ userName: email })
                .then((result) => {
                    // sign in with external authority?
                    if (result.signInUri) {
                        window.location.href = result.signInUri;
                        return;
                    }          
                    if (result.status === "Invited") {
                        dispatch(setInvitationInfo(result.invitation));
                        dispatch(push("/auth/register"));
                    }
                    else if (result.status === "Registered") {
                        dispatch(setRegistrationInfo(result.registration));
                        dispatch(push("/auth/password"));
                    }
                    else if (result.status === "EmailUnconfimed") {
                        api.accountSendEmailConfirmationToken({
                            command: {
                                email: result.userName
                            }
                        }).then((result) => {
                            dispatch(push("/auth/confirm-email-sent"));
                            dispatch(endSubmitting());
                        }).catch((exception) => {
                            dispatch(error("Error_Sending_Email"))
                            dispatch(endSubmitting());
                            });
                    }
                    else if (result.status === "LicenseExpired") {
                        dispatch(setLicenseInfo(result.license));
                        dispatch(push("/auth/expired"));
                    }
                    else {
                        dispatch(push("/auth/demo"));
                    }
                    dispatch(endSubmitting());
                }).catch((exception) => {
                    dispatch(error("Email_Validation_Error", exception))
                    dispatch(shakeForm());
                    dispatch(endSubmitting());
                });
        }
    }
}

