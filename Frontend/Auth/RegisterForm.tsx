import * as React from "react"
import classnames = require("classnames");
import { connectedForm, FormField, bind, UpdateField } from "../Commons/forms"
import { LaddaButton } from "../Tags/LaddaButton"
import { setPassword, scorePassword, setPasswordVisible, setRedirectUri, setTerms, shakeForm, startSubmitting, endSubmitting, loginWithToken } from "./Actions"
import { InvitationInfo } from "../Api/api"
import { push } from 'react-router-redux'
import { AccountApi, API_CLIENT_ID } from "../Api/api"
import { error } from "../Commons/actions"
import { t, thtml } from "../i18n/translate"
import { reportException, trackTrace } from "../Commons/ai"
import { rememberAutofillData } from "./actions"

export interface RegisterFormProps {
    email: FormField<string>;
    inviteToken: string;
    password: FormField<string>;
    passwordScore: number;
    passwordVisible: boolean;
    terms: FormField<boolean>;
    invitation: InvitationInfo;
    isShaking: boolean;
    isSubmitting: boolean;

    setPassword: UpdateField<string>;
    setTerms: UpdateField<boolean>;
    scorePassword: (password: string) => void;
    setPasswordVisible: (visibility: boolean) => void;    
    registerUser: (formData: RegisterFormProps) => void;
    showEmailForm: () => void;
}

const boundState = (state) => state.auth;
const boundProps = ['email', 'inviteToken', 'password', 'passwordScore', 'passwordVisible', 'terms', 'invitation', 'isShaking', 'isSubmitting'];
const boundActions = { setPassword, scorePassword, setPasswordVisible, setTerms, registerUser, showEmailForm }

export default connectedForm(boundState, boundProps, boundActions)(
    class RegisterForm extends React.Component<RegisterFormProps, {}> {
        scoreDesc(score: number) {
            switch (score) {
                case null:
                    return { class: "progress-bar-danger", width: "0%", desc: null }
                case 0:
                    return { class: "progress-bar-danger", width: "0%", desc: "Very weak" }
                case 1:
                    return { class: "progress-bar-danger", width: "25%", desc: "Weak" }
                case 2:
                    return { class: "progress-bar-warning", width: "50%", desc: "So-so" }
                case 3:
                    return { class: "progress-bar-info", width: "75%", desc: "Good" }
                default:
                    return { class: "progress-bar-success", width: "100%", desc: "Great!" }
            }
        }
        submit(e) {
            e.preventDefault();
            this.props.registerUser(this.props);
        }
        componentDidMount() {
            setTimeout(() => {
                requirejs(["zxcvbn"], function (zxcvbn) {
                    //console.log("preload");
                });
            }, 100);
            
            if (!(this.props.email.value || this.props.inviteToken)) {
                this.props.showEmailForm();
            }
            trackTrace("RegisterForm", { email: this.props.email.value, inviteToken: this.props.inviteToken });
        }
        updatePassword(password: string) {
            this.props.setPassword({
                value: password
            });
            this.props.scorePassword(password);
        }
        togglePasswordVisibility() {
            this.props.setPasswordVisible(!this.props.passwordVisible);
        }
        toggleTerms() {
            this.props.setTerms({ value: !this.props.terms.value });
        }
        render() {
            let score = this.scoreDesc(this.props.passwordScore);
            
            return (
                <div>
                    <h1 className="login-title">
                        {thtml("Auth:Welcome to a network message", { "networkName": this.props.invitation.networkName })}                        
                    </h1>
                    <form className="form-signin text-left" onSubmit={(e) => this.submit(e)}>
                        <input type="text" name="email" value={this.props.email.value} className="hidden" onChange={e => { } } />
                        <div className={ classnames({ "shake": this.props.isShaking }) }>
                            <div className={
                                classnames({
                                    "form-group has-feedback": true,
                                    "has-error": this.props.password.hasError,                                
                                }) }>
                                <label htmlFor="disabledTextInput">{t("Auth:Set your password for signin in")}</label>
                                <input type={ this.props.passwordVisible ? "text" : "password" }
                                    className="form-control input-lg" placeholder={t("Password")} autoFocus
                                    onChange={bind( value => this.updatePassword(value)) } 
                                    />
                                <i className="glyphicon glyphicon-eye-open form-control-feedback hide"
                                    style={{ cursor: "pointer", pointerEvents: "all" }}
                                    onClick={ () => this.togglePasswordVisibility() }
                                    ></i>

                                { this.props.password.hasError &&
                                    <span className="help-block">
                                    {t("Auth:Password must be at least 6 characters long")}
                                    </span>
                                }

                                <div className="progress password-strength ">
                                    <div className={ "progress-bar " + score.class } role="progressbar"
                                        style={{ width: score.width }}>
                                    </div>                            
                                </div>
                                { score.desc &&
                                    <div className="password-strength-label">
                                    {t("Auth:Password strength")}: { t("Auth:"+score.desc) }
                                    </div>
                                }                            
                            </div>
                            <div className="form-group text-left" style={{
                                paddingTop: "10px"
                            }}>
                                <div className={
                                    classnames({
                                        "form-group": true,
                                        "has-error": this.props.terms.hasError,
                                    }) }>
                                    <label className="checkbox">
                                        <input type="checkbox" name="terms" className="checkbox"
                                            checked={this.props.terms.value}                                            
                                            onClick={ (e) => this.toggleTerms() }
                                            />
                                        <span>                                            
                                            <div dangerouslySetInnerHTML={{
                                                __html: t("Auth:Terms and conditions")
                                                + ((this.props.invitation && this.props.invitation.additionalTerms) ?
                                                    " " + this.props.invitation.additionalTerms : "")
                                            }}/>                                            
                                        </span>                                    
                                    </label>
                                </div>
                            </div>
                            <LaddaButton loading={this.props.isSubmitting}
                                className="btn btn-lg btn-primary btn-block"
                                type="submit">{t("Auth:Sign up")}</LaddaButton>
                        </div>
                    </form>
                </div>
            )
        }
    }
)


function showEmailForm() {
    return dispatch => {
        dispatch(push("/auth"))
    }
}

/// registers a user priously invited to the app
function registerUser(formData: RegisterFormProps) {
    return dispatch => {
        let valid = true;
        if (!(formData.terms.value || $('input[name=terms]').is(':checked'))) {
            dispatch(setTerms({ value: formData.terms.value, hasError: true }));
            valid = false;
        }
        if (!formData.password.value || formData.password.value.length < 6) {
            dispatch(setPassword({ value: formData.password.value, hasError: true }));
            valid = false;
        }
        if (!valid) {
            dispatch(shakeForm());
        }
        else {
            dispatch(startSubmitting());

            let api = new AccountApi();

            if (formData.inviteToken) {

                // register with token
                api.accountRegisterWithToken({
                    command: {
                        token: formData.inviteToken,
                        password: formData.password.value,
                        apiClientId: API_CLIENT_ID
                    }
                }).then((token) => {
                    dispatch(loginWithToken(token));
                    dispatch(endSubmitting());                    
                })
                .catch((exception) => {                    
                    dispatch(shakeForm());
                    dispatch(endSubmitting());                    
                    return exception.json();
                })
                .then(err => {
                    dispatch(error("User_Registering_Error", err.Message));
                    reportException(err);
                });
            }
            else {

                // register with username and password
                api.accountRegisterUser({
                    command: {
                        email: formData.email.value,
                        password: formData.password.value
                    }
                })
                .then((result) => {
                    api.accountSendEmailConfirmationToken({
                        command: {
                            email: formData.email.value
                        }
                    }).then((result) => {
                        dispatch(endSubmitting());
                        rememberAutofillData(formData.email.value, formData.password.value, "/auth/confirm-email-sent");
                    }).catch((exception) => {
                        dispatch(error("Error_Sending_Email"))
                        dispatch(endSubmitting());
                    });
                }).catch((exception) => {                                            
                    dispatch(shakeForm());
                    dispatch(endSubmitting());
                    return exception.json();
                })
                .then(err => {
                    dispatch(error("User_Registering_Error", err.Message));
                    reportException(err);
                });
            }
        }
    }
}

