import * as React from "react"
import classnames = require("classnames");
import { connectedForm, bind, UpdateField, FormField } from "../Commons/forms"
import { setPassword, scorePassword, setPasswordVisible, setEmail, 
    startSubmitting, endSubmitting, shakeForm, setPasswordToken, loginWithToken } from "./Actions"
import { LaddaButton } from "../Tags/LaddaButton"
import { Link } from 'react-router'
import { AccountApi, API_CLIENT_ID, UserStatus } from "../Api/api"
import { t, thtml } from "../i18n/translate"

export interface PasswordResetFormProps {
    location: HistoryModule.Location;
    passwordToken: FormField<string>;
    password: FormField<string>;
    passwordScore: number;
    passwordVisible: boolean;    
    isShaking: boolean;
    isSubmitting: boolean;

    validatePasswordResetToken: (userId: string, token: string) => void;
    setPassword: UpdateField<string>;
    scorePassword: (password: string) => void;
    setPasswordVisible: (visibility: boolean) => void;
    resetPassword: (userId: string, token: string, newPassword: string) => void
}

const boundState = (state) => state.auth;
const boundProps = ['passwordToken', 'password', 'passwordScore', 'passwordVisible', 'isShaking', 'isSubmitting'];
const boundActions = { validatePasswordResetToken, setPassword, scorePassword, setPasswordVisible, resetPassword }

export default connectedForm(boundState, boundProps, boundActions)(
    class PasswordResetForm extends React.Component<PasswordResetFormProps, {}> {
        getUserId() {
            return this.props.location.query["userId"];
        }
        getToken() {
            return this.props.location.query["token"];
        }
        componentDidMount() {
            this.props.validatePasswordResetToken(this.getUserId(), this.getToken());

            setTimeout(() => {
                requirejs(["zxcvbn"], function (zxcvbn) {
                    //console.log("preload");
                });
            }, 100);
        }
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
            this.props.resetPassword(this.getUserId(), this.getToken(), this.props.password.value);
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
        render() {
            let score = this.scoreDesc(this.props.passwordScore);
            return (
                <div>
                     
                    { typeof (this.props.passwordToken.hasError) === "undefined" &&
                        <div>
                            <h1 className="login-title">
                                {t("Auth:Validating password reset link")}                                
                            </h1>
                            <LaddaButton loading={true}
                                className="btn btn-lg btn-default" buttonStyle="contract"
                                type="submit">{t("Auth:Validating")}</LaddaButton>
                        </div>
                    }
                    { this.props.passwordToken.hasError === false &&
                        <div>
                            <h1 className="login-title">
                            {t("Auth:Choose your new password")}
                            </h1>
                            <form className="form-signin text-left" onSubmit={ (e) => this.submit(e) }>
                                <div className={ classnames({ "shake": this.props.isShaking }) }>
                                    <div className={
                                        classnames({
                                            "form-group has-feedback": true,
                                            "has-error": this.props.password.hasError,
                                        }) }>
                                        <label htmlFor="disabledTextInput">{t("Auth:Set your password for signin in")}</label>
                                        <input type={ this.props.passwordVisible ? "text" : "password" }
                                            className="form-control input-lg" placeholder={t("Auth:Password")} autoFocus
                                            onChange={bind(value => this.updatePassword(value)) }
                                            />
                                        
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
                                            <div className="password-strength-label" style={{
                                                paddingBottom: 10
                                            }}>
                                                {t("Auth:Password strength")}: { t("Auth:"+score.desc) }
                                            </div>
                                        }
                                    </div>                                
                                    <LaddaButton loading={this.props.isSubmitting}
                                        className="btn btn-lg btn-primary btn-block"
                                        type="submit">{t("Auth:Reset password")}</LaddaButton>
                                </div>
                            </form>
                        </div>
                    }
                    { this.props.passwordToken.hasError === true &&
                        <div className="has-error">
                            <h1 className="login-title">                        
                                {t("Auth:The link has expired and can no longer be used to reset your password")}.
                            </h1>
                            <Link to="/auth/recover-password" className="btn btn-lg btn-default">
                                {t("Auth:Send me the link again")}
                            </Link>
                        </div>
                    }
                
                </div>
            )
        }
    }
)

function validatePasswordResetToken(userId: string, token: string) {
    return dispatch => {
        dispatch(startSubmitting());

        let api = new AccountApi();
        api.accountValidatePasswordResetToken({
            command: {
                userId: userId,
                token: token
            }
        }).then((result) => {
            if (result.tokenValid) {
                dispatch(setEmail({
                    value: result.userName, hasError: false
                }));
                dispatch(setPasswordToken({ value: token, hasError: false }));
            } else {
                dispatch(setPasswordToken({ value: token, hasError: true }));
                dispatch(shakeForm());
            }
            dispatch(endSubmitting());
        }).catch((exception) => {
            dispatch(setPasswordToken({ value: token, hasError: true }));
            dispatch(shakeForm());
            dispatch(endSubmitting());
        });
    }
}

function resetPassword(userId: string, token: string, newPassword: string) {
    return dispatch => {
        if (!newPassword || newPassword.length < 6) {
            dispatch(setPassword({ value: newPassword, hasError: true }));
            dispatch(shakeForm());
        }
        else {
            dispatch(startSubmitting());

            let api = new AccountApi();
            api.accountResetPassword({
                command: {
                    userId, token, newPassword,
                    apiClientId: API_CLIENT_ID
                }
            }).then((token) => {
                dispatch(loginWithToken(token));
            }).catch((exception) => {
                dispatch(setPasswordToken({ value: token, hasError: true }));
                dispatch(shakeForm());
                dispatch(endSubmitting());
            });
        }
    }
}

