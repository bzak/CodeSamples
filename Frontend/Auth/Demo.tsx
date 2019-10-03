import * as React from "react"
import { connectedForm, bindFormField, UpdateField, FormField } from "../Commons/forms"
import { setEmail, setName, setCompany, setPhoneNumber, startSubmitting, shakeForm, endSubmitting } from "./Actions"
import { push } from 'react-router-redux'
import { AccountApi } from "../Api/api"
import { LaddaButton } from "../Tags/LaddaButton"
import { t, thtml } from "../i18n/translate"

interface DemoFormProps  {
    email: FormField<string>,
    name: FormField<string>,
    company: FormField<string>,
    phoneNumber: FormField<string>,
    isShaking: boolean;
    isSubmitting: boolean;    

    setEmail: UpdateField<string>;
    setName: UpdateField<string>;
    setCompany: UpdateField<string>;
    setPhoneNumber: UpdateField<string>;
    submitDemo: (formData: DemoFormProps) => void;
}

const boundState = (state) => state.auth;
const boundProps = ['email', 'name', 'company','phoneNumber', 'isShaking', 'isSubmitting'];
const boundActions = { setEmail, setName, setCompany, setPhoneNumber, submitDemo };

export default connectedForm(boundState, boundProps, boundActions)(
    class DemoSignUp extends React.Component<DemoFormProps, {}> {
        submit(e) {
            e.preventDefault();
            this.props.submitDemo(this.props);
        }
        render() {
            return (
                <div>
                    <h1 className="login-title">{thtml("Auth:Sign up for an online demo with our consultant and a free trial")}</h1>
                    <form className="form-signin text-left"onSubmit={ (e) => this.submit(e) } >
                        <div className="form-group">
                            <label htmlFor="disabledTextInput">{t("Auth:Email address")}</label>                            
                            <input type="text" className="form-control input-lg" 
                                value={this.props.email.value} name="email"
                                onChange={ bindFormField(this.props.setEmail) }/>
                        </div>
                        <div className="form-group">
                            <label htmlFor="disabledTextInput">{t("Auth:Your name")}</label>                            
                            <input type="text" className="form-control input-lg" placeholder="Your name" autoFocus
                                value={this.props.name.value} name="name"
                                onChange={ bindFormField(this.props.setName) }/>
                        </div>
                        <div className="form-group">
                            <label htmlFor="disabledTextInput">{t("Auth:Company")}</label>                            
                            <input type="text" className="form-control input-lg" placeholder="Company name"
                                value={this.props.company.value} name="company"
                                onChange={ bindFormField(this.props.setCompany) }/>
                        </div>
                        <div className="form-group">
                            <label htmlFor="disabledTextInput">{t("Auth:Phone number")}</label>                            
                            <input type="text" className="form-control input-lg" placeholder="+49 555 555 555"
                                value={this.props.phoneNumber.value} name="phoneNumber"
                                onChange={ bindFormField(this.props.setPhoneNumber) }/>
                        </div>
                        <LaddaButton loading={this.props.isSubmitting}
                            className="btn btn-lg btn-primary btn-block"
                            type="submit">{t("Auth:Sign up")}</LaddaButton>
                    </form>
                </div>
            )
        }
    }
)

function submitDemo(form: DemoFormProps) {
    return dispatch => {
        dispatch(startSubmitting());

        let api = new AccountApi();
        api.accountRegisterForDemo({
            command: {
                email: form.email.value,
                company: form.company.value,
                name: form.name.value,
                phoneNumber: form.phoneNumber.value
            }
        })
            .then((result) => {
                dispatch(push("/auth/demo-submitted"));
                dispatch(endSubmitting());
            })
            .catch((error) => {
                dispatch(shakeForm());
                dispatch(endSubmitting());
            });
    }
}

