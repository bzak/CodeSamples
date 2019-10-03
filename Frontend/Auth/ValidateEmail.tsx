import * as React from "react"
import classnames = require("classnames");
import { connectedForm, validateEmail, bindFormField, UpdateField, FormField } from "../Commons/forms"
import { LaddaButton } from "../Tags/LaddaButton"
import { setEmail, setInviteToken, setRedirectUri, shakeForm, startSubmitting, endSubmitting, setRegistrationInfo, setInvitationInfo, setLicenseInfo } from "./Actions"
import { replace } from 'react-router-redux'
import { AccountApi } from "../Api/api"
import { error } from "../Commons/actions"
import { t, thtml } from "../i18n/translate"

interface EmailFormProps  {
    location: HistoryModule.Location;
    
    isShaking: boolean;
    isSubmitting: boolean;    

    setEmail: UpdateField<string>;
    setRedirectUri: (uri:string) => void 
    submitEmail: (email: string) => void 
    showEmailForm: () => void 
}

const boundState = (state) => state.auth;
const boundProps = [ 'isShaking', 'isSubmitting'];
const boundActions = { submitEmail, setRedirectUri, setEmail, showEmailForm }

export default connectedForm(boundState, boundProps, boundActions)(
    class ValidateInvite extends React.Component<EmailFormProps, {}> {            
        getRedirectUri() {
            return this.props.location.query["redirect_uri"];
        }
        getEmail() {
            return this.props.location.query["email"];
        }
        componentDidMount() {
            if (this.getRedirectUri())
                this.props.setRedirectUri(this.getRedirectUri());

            let email = this.getEmail();       
            if (validateEmail(email)) {
                this.props.setEmail({ value: email, hasError: false });
                this.props.submitEmail(email);
            } else {
                if (email)
                    this.props.setEmail({ value: email, hasError: true });
                else
                    this.props.setEmail({ value: null, hasError: false });
                this.props.showEmailForm();
            }
        }
        render() {
            return (
                <div>
                    <div>
                        <h1 className="login-title">
                            {t("Auth:Validating your email")}...
                        </h1>
                        <LaddaButton loading={true}
                            className="btn btn-lg btn-primary" buttonStyle="contract"
                            type="submit">...</LaddaButton>
                    </div>
                </div>
            )
        }
    }
)

function showEmailForm() {    
    return dispatch => {
        dispatch(replace("/auth"))
    };
}

function submitEmail(email: string) {
    return dispatch => {
        if (!validateEmail(email)) {
            dispatch(shakeForm());
            dispatch(setEmail({ value: email, hasError: true }));
        } else {
            dispatch(startSubmitting());

            let api = new AccountApi();
            api.accountGetUserStatus({ userName: email })
                .then((result) => {
                    if (result.status === "Invited") {
                        dispatch(setInvitationInfo(result.invitation));
                        dispatch(replace("/auth/register"));
                    }
                    else if (result.status === "Registered") {
                        dispatch(setRegistrationInfo(result.registration));
                        dispatch(replace("/auth/password"));
                    }
                    else if (result.status === "EmailUnconfimed") {
                        api.accountSendEmailConfirmationToken({
                            command: {
                                email: result.userName
                            }
                        }).then((result) => {
                            dispatch(replace("/auth/confirm-email-sent"));
                            dispatch(endSubmitting());
                        }).catch((exception) => {
                            dispatch(error("Error_Sending_Email"))
                            dispatch(endSubmitting());
                        });
                    }
                    else if (result.status === "LicenseExpired") {
                        dispatch(setLicenseInfo(result.license));
                        dispatch(replace("/auth/expired"));
                    }
                    else {
                        dispatch(replace("/auth/demo"));
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

