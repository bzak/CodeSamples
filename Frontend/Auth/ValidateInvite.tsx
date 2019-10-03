import * as React from "react"
import classnames = require("classnames");
import { connectedForm, validateEmail, bindFormField, UpdateField, FormField } from "../Commons/forms"
import { LaddaButton } from "../Tags/LaddaButton"
import { setEmail, setInviteToken, setRedirectUri, shakeForm, startSubmitting, endSubmitting, setRegistrationInfo, setInvitationInfo, setLicenseInfo } from "./Actions"
import { push } from 'react-router-redux'
import { AccountApi } from "../Api/api"
import { error } from "../Commons/actions"
import { t, thtml } from "../i18n/translate"

interface EmailFormProps  {
    location: HistoryModule.Location;
    params: {
        token: string;        
    }
    inviteToken: FormField<string>;
    isShaking: boolean;
    isSubmitting: boolean;    
    redirectUri: string;

    setEmail: UpdateField<string>;
    setRedirectUri: (uri:string) => void 
    submitToken: (token: string, redirect: string) => void 
}

const boundState = (state) => state.auth;
const boundProps = ['inviteToken', 'redirectUri', 'isShaking', 'isSubmitting'];
const boundActions = { submitToken, setRedirectUri }

export default connectedForm(boundState, boundProps, boundActions)(
    class ValidateInvite extends React.Component<EmailFormProps, {}> {            
        getRedirectUri() {
            return this.props.location.query["redirect_uri"];
        }
        componentDidMount() {            
            this.props.submitToken(this.props.params.token, this.getRedirectUri());
        }
        render() {
            return (
                <div>
                    <div>
                        <h1 className="login-title">
                            {t("Auth:Validating the invitation link")}                            
                        </h1>
                        <LaddaButton loading={true}
                            className="btn btn-lg btn-primary" buttonStyle="contract"
                            type="submit">{t("Auth:Validating")}</LaddaButton>
                    </div>
                </div>
            )
        }
    }
)

function submitToken(token: string, redirect: string) {
    return dispatch => {        
        dispatch(startSubmitting());
        if (redirect) {
            dispatch(setRedirectUri(redirect));
        }
        let api = new AccountApi();
        api.accountGetUserStatus({ invitationToken: token })
            .then((result) => {
                // sign in with external authority?
                if (result.signInUri) {
                    window.location.href = result.signInUri;
                    return;
                }                                
                if (result.status === "Invited") {
                    dispatch(setEmail({ value: result.userName, hasError: false }));
                    dispatch(setInviteToken(token));
                    dispatch(setInvitationInfo(result.invitation));
                    if (!redirect) {
                        dispatch(setRedirectUri("/?networkId=" + result.invitation.networkId));
                    }
                    dispatch(push("/auth/register"));
                }
                else if (result.status === "Registered") {
                    dispatch(setEmail({ value: result.userName, hasError: false }));
                    dispatch(setInviteToken(token));
                    if (!redirect) {
                        dispatch(setRedirectUri("/?networkId=" + result.invitation.networkId));
                    }
                    dispatch(setRegistrationInfo(result.registration));
                    
                    dispatch(push("/auth/password"));
                }
                else if (result.status === "LicenseExpired") {
                    dispatch(setLicenseInfo(result.license));
                    dispatch(push("/auth/expired"));
                }
                else {
                    dispatch(push("/auth/"));
                }
                dispatch(endSubmitting());
            }).catch((exception) => {
                dispatch(error("Token_Validation_Error"))
                dispatch(shakeForm());
                dispatch(endSubmitting());
                dispatch(push("/auth/"));
            });        
    }
}

